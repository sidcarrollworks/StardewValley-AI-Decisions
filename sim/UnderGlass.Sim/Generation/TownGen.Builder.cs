namespace UnderGlass.Sim.Generation;

public static partial class TownGen
{
    /// <summary>A generated person while the town is built.</summary>
    private sealed class Gen
    {
        public string Name = "", Surname = "", Hood = "", Kind = "";
        public int Slot, Age, Plot;
        public bool Female;
        public Villager Card = null!;
        public Temperament Traits = null!;
        public Body Body = null!;
        public Job? Job;
        public string? Workplace;
        public readonly List<Haunt> Haunts = new();
        public readonly Dictionary<string, double> Acts = new();
        public readonly List<string> Friends = new();
        public readonly Dictionary<string, Kin> Family = new();
        public YearDay Birthday;
        public Tile Step;
        public string Household => Surname;
        public string Home => "Home:" + Surname;
        public Stage Stage => Ages.StageOf(Age);
    }

    private sealed class Household
    {
        public required string Surname { get; init; }
        public required string Hood { get; init; }
        public required int Slot { get; init; }
        public required Tile Step { get; init; }
        public required string Shape { get; init; }
        public readonly List<Gen> Members = new();
    }

    /// <summary>A job place a slot brings (town spec 4.3, step 7): where, the hours, effort and pay,
    /// and who pays (a household, the town, or null for outside money).</summary>
    private sealed record Post(string Place, string Role, int Start, int End, int[] DaysOff, double Effort, double Wage, string? PaidBy, bool Keeper = false);

    private sealed class Builder
    {
        private readonly TownSpec _spec;
        private readonly TownData _core;
        private readonly List<Location> _places;
        private readonly List<Link> _links;
        private readonly List<Gathering> _hubs = new();
        private readonly Router _router = new();
        private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<Tile>> _spots = new();
        private readonly List<Gen> _people = new();
        private readonly List<Household> _households = new();
        private readonly Dictionary<(string, string), double> _tensions = new();
        private readonly Dictionary<string, string> _keepers = new();
        private readonly Dictionary<string, Tile> _patrol = new();
        private readonly List<(string Who, double PerWeek, string? From)> _incomes = new();
        private readonly Dictionary<string, double> _allowances = new();
        private readonly Dictionary<string, string> _groceries = new();
        private readonly Dictionary<string, (double, double)> _wants = new();
        private readonly Dictionary<string, double> _purses = new();
        private readonly List<(string Hood, Tile Linger, int Radius)> _greens = new();
        private readonly List<string> _publicPlaces = new();
        private readonly Dictionary<string, (double Mean, double Sd)[]> _spread;
        // The core cast's traits as a lower-triangular factor of their covariance, for wildcards.
        private readonly double[,] _wildFactor;

        public Builder(TownSpec spec, TownData core)
        {
            _spec = spec;
            _core = core;
            _places = core.Places.ToList();
            _links = core.Links.ToList();
            foreach (Location p in _places)
                _router.Add(p);
            foreach (Link l in _links)
                _router.Add(l);
            foreach (Villager v in core.Cast)
                _names.Add(v.Name);
            foreach (string r in Names.Reserved)
                _names.Add(r);
            foreach (Location p in core.Places)
                _names.Add(p.Name.StartsWith("Home:", StringComparison.Ordinal) ? p.Name[5..] : p.Name);
            // Spots already taken in the core's public places: the cast's own work and haunt spots.
            foreach (Villager v in core.Cast)
            {
                if (v.Job is { } j)
                    Take(j.Place, j.Spot);
                foreach (Haunt h in v.Haunts)
                    Take(h.Place, h.Spot);
            }
            _publicPlaces.AddRange(new[] { "Square", "Beach", "Saloon", "Store", "Mart", "ClinicYard" });
            _spread = new Dictionary<string, (double, double)[]> { ["core"] = Spread(core.Cast) };
            _wildFactor = Cholesky(Covariance(core.Cast));
        }

        private double U(params string[] parts) => Rng.Unit(_spec.Seed, new[] { "town" }.Concat(parts).ToArray());
        private int R(int min, int max, params string[] parts) => min + (int)Math.Floor(U(parts) * (max - min + 1));
        /// <summary>A standard normal draw (Box-Muller over two keyed draws).</summary>
        private double N(params string[] parts)
        {
            double u1 = Math.Max(1e-12, U(parts.Append("n1").ToArray())), u2 = U(parts.Append("n2").ToArray());
            return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        }

        private void Take(string place, Tile t)
        {
            if (!_spots.TryGetValue(place, out var list))
                _spots[place] = list = new List<Tile>();
            list.Add(t);
        }

        private Location Place(string name) => _places.First(p => p.Name == name);

        private void AddPlace(Location p)
        {
            _places.Add(p);
            _router.Add(p);
        }

        private void AddLink(Link l)
        {
            _links.Add(l);
            _router.Add(l);
        }

        // ---- the neighbourhood -------------------------------------------------------------

        public void Grow(Slot slot, int k)
        {
            Template t = slot.Template == "Green" ? Templates.Green(slot.Homes) : Templates.Lane(slot.Homes);
            var hood = new Location(slot.Name, true, t.Rows);
            string road = slot.Label.Split(' ')[0] + "Road";
            AddPlace(hood);
            AddPlace(new Location(road, true, Enumerable.Repeat(new string('.', slot.ConnectorLength), 3).ToList()));
            AddLink(new Link(slot.JoinPlace, slot.JoinTile, road, new Tile(0, 1)));
            AddLink(new Link(road, new Tile(slot.ConnectorLength - 1, 1), slot.Name, t.Entry));
            _greens.Add((slot.Name, t.Linger, t.LingerRadius));
            _patrol[slot.Name] = t.Linger;
            _keepers[slot.Name] = DefaultTown.Mayor; // the mayor keeps the greens, as he keeps the square
            foreach (Tile b in t.Benches.Append(t.Linger))
                Take(slot.Name, b);

            // Households onto plots: about one home in ten stays empty, chosen by draw.
            int occupied = slot.Homes - Math.Max(1, (int)Math.Round(slot.Homes * 0.1));
            var plots = Enumerable.Range(0, slot.Homes).OrderBy(i => U("empty", slot.Code, i.ToString())).Take(occupied).OrderBy(i => i).ToList();
            int[] sizes = Sizes(slot, plots.Count);
            var surnames = Pool(Names.Surnames, slot, k);
            var women = Pool(Names.Women, slot, k);
            var men = Pool(Names.Men, slot, k);
            var houses = new List<Household>();
            for (int h = 0; h < plots.Count; h++)
            {
                string surname = Draw(surnames, Names.Surnames, "surname", slot.Code, plots[h].ToString());
                var house = new Household { Surname = surname, Hood = slot.Name, Slot = k, Step = t.Steps[plots[h]], Shape = Shape(sizes[h], slot.Code, plots[h]) };
                var (home, door) = Templates.Home("Home:" + surname, sizes[h]);
                AddPlace(home);
                AddLink(new Link(slot.Name, house.Step, home.Name, door));
                Members(house, sizes[h], women, men, slot.Code, plots[h]);
                foreach (Gen g in house.Members)
                {
                    g.Slot = k;
                    g.Hood = slot.Name;
                    g.Plot = plots[h];
                    g.Step = house.Step;
                }
                houses.Add(house);
            }
            _households.AddRange(houses);
            KinAcross(houses, k);
            foreach (Household house in houses)
                foreach (Gen g in house.Members)
                    Character(g, house, slot.Code);
            Livelihoods(slot, t, houses);
            foreach (Household house in houses)
                foreach (Gen g in house.Members)
                {
                    Haunts(g, slot, t, k);
                    Acts(g, house, slot.Code);
                }
            foreach (Household house in houses)
                foreach (Gen g in house.Members)
                    Friends(g, k);
            Tensions(houses, slot.Code);
            foreach (Household house in houses)
            {
                _purses[house.Surname] = 200 + 200 * house.Members.Count;
                _groceries[house.Surname] = U("grocer", slot.Code, house.Surname) < 0.3 ? "Mart" : "Store";
            }
        }

        /// <summary>Household sizes for one slot (town spec 4.3, step 3): drawn from the census shares
        /// (1: 30%, 2: 34%, 3: 16%, 4: 13%, 5: 7%), then evened out to the slot's people exactly.</summary>
        private int[] Sizes(Slot slot, int households)
        {
            double[] cumulative = { 0.30, 0.64, 0.80, 0.93, 1.0 };
            var sizes = new int[households];
            for (int h = 0; h < households; h++)
            {
                double u = U("size", slot.Code, h.ToString());
                sizes[h] = 1 + Array.FindIndex(cumulative, c => u < c);
            }
            int diff = slot.People - sizes.Sum();
            for (int guard = 0; diff != 0 && guard < 1000; guard++)
            {
                // The household to change is chosen by a keyed order, smallest first when growing.
                var order = Enumerable.Range(0, households).OrderBy(h => diff > 0 ? sizes[h] : -sizes[h])
                    .ThenBy(h => U("even", slot.Code, h.ToString(), guard.ToString())).ToList();
                int pick = order.First(h => diff > 0 ? sizes[h] < 6 : sizes[h] > 1);
                sizes[pick] += Math.Sign(diff);
                diff -= Math.Sign(diff);
            }
            return sizes;
        }

        /// <summary>This slot's share of a name list: every slot draws from its own keyed part of it,
        /// so a neighbourhood's names don't depend on the ones after it.</summary>
        private List<string> Pool(IReadOnlyList<string> names, Slot slot, int k)
            => names.Where(n => !_names.Contains(n) && (int)(Rng.Hash("name-slot", n) % 6) == k % 6)
                .OrderBy(n => U("pool", slot.Code, n)).ToList();

        private string Draw(List<string> pool, IReadOnlyList<string> source, params string[] key)
        {
            if (pool.Count == 0)
            {
                // A slot's own share ran out (a rare town with many of one sex): take from the whole
                // list. Such a town no longer nests its later slots' names exactly.
                pool.AddRange(source.Where(n => !_names.Contains(n)).OrderBy(n => U("refill", n)));
                if (pool.Count == 0)
                    throw new InvalidOperationException("ran out of names for " + string.Join(" ", key));
            }
            int i = (int)Math.Floor(U(key) * pool.Count);
            string name = pool[i];
            pool.RemoveAt(i);
            _names.Add(name);
            return name;
        }

        /// <summary>A household's shape by its size (town spec 4.3, step 3).</summary>
        private string Shape(int size, string code, int plot)
        {
            double u = U("shape", code, plot.ToString());
            return size switch
            {
                1 => u < 0.25 ? "elder" : "single",
                2 => u < 0.6 ? "couple" : u < 0.75 ? "parent" : u < 0.9 ? "elders" : "housemates",
                3 => u < 0.7 ? "family" : u < 0.8 ? "parent" : u < 0.9 ? "grandchild" : "housemates",
                _ => u < 0.9 ? "family" : "parent",
            };
        }

        private static string KindOf(int age, bool female) => age < 13 ? (female ? "girl" : "boy")
            : age <= 30 ? (female ? "young woman" : "young man")
            : age < 45 ? (female ? "woman" : "man")
            : age < 65 ? (female ? "older woman" : "older man")
            : (female ? "old woman" : "old man");

        /// <summary>The members of a household, their ages and kin (town spec 4.3, step 4): couples
        /// 21-55 within N(2, 3) years of each other, parents 22-40 at a child's birth, children 5-12,
        /// teens 13-19, elders 65-85; one family with children in about seven has a stepparent.</summary>
        private void Members(Household house, int size, List<string> women, List<string> men, string code, int plot)
        {
            string P(params string[] more) => string.Join("/", new[] { code, plot.ToString() }.Concat(more));
            Gen Person(int age, bool female, string role)
            {
                var g = new Gen { Age = age, Female = female, Surname = house.Surname, Kind = KindOf(age, female) };
                g.Name = Draw(female ? women : men, female ? Names.Women : Names.Men, "first", P(role));
                house.Members.Add(g);
                return g;
            }
            bool Female(string role) => U("sex", P(role)) < 0.5;
            void Tie(Gen a, Kin kin, Gen b) { b.Family[a.Name] = kin; a.Family[b.Name] = Ages.Reverse(kin); }
            int Adult(string role, int min = 21, int max = 55) => min + (int)Math.Floor(U("age", P(role)) * (max - min + 1));
            switch (house.Shape)
            {
                case "single":
                    Person(Adult("a", 21, 64), Female("a"), "a");
                    break;
                case "elder":
                    Person(Adult("a", 65, 85), Female("a"), "a");
                    break;
                case "housemates":
                    for (int i = 0; i < size; i++)
                        Person(Adult("m" + i, 21, 40), Female("m" + i), "m" + i);
                    break;
                case "elders":
                {
                    bool f = Female("a");
                    Gen a = Person(Adult("a", 65, 85), f, "a");
                    bool sameSex = U("same-sex", P()) < 0.08;
                    Gen b2 = Person(Math.Clamp(a.Age + (int)Math.Round(2 + 3 * N("gap", P())), 65, 90), sameSex ? f : !f, "b");
                    Tie(a, Kin.Spouse, b2);
                    break;
                }
                default:
                {
                    // couple, family, parent, grandchild
                    bool grand = house.Shape == "grandchild";
                    bool f = Female("a");
                    Gen a = Person(grand ? Adult("a", 65, 80) : Adult("a"), f, "a");
                    Gen? partner = null;
                    if (house.Shape is "couple" or "family" || grand && size >= 3)
                    {
                        bool sameSex = U("same-sex", P()) < 0.08;
                        int age = Math.Clamp(a.Age + (int)Math.Round(2 + 3 * N("gap", P())), grand ? 63 : 21, grand ? 90 : 64);
                        partner = Person(age, sameSex ? f : !f, "b");
                        Tie(a, Kin.Spouse, partner);
                    }
                    int kids = size - (partner is null ? 1 : 2);
                    bool blended = !grand && partner is not null && kids > 0 && U("blended", P()) < 0.15;
                    // A child is born when each full parent was 22 to 40 (a stepparent's age doesn't
                    // bound it; a grandchild was born to the parent in between).
                    bool both = !grand && !blended && partner is not null;
                    int younger = both ? Math.Min(a.Age, partner!.Age) : a.Age, older = both ? Math.Max(a.Age, partner!.Age) : a.Age;
                    int eldest = grand ? 17 : Math.Min(19, younger - 22), youngest = grand ? 5 : Math.Max(5, older - 40);
                    var children = new List<Gen>();
                    for (int c = 0; c < kids; c++)
                    {
                        if (eldest < youngest)
                        {
                            Person(Adult("x" + c, 21, 40), Female("x" + c), "x" + c); // no child fits: a lodger
                            continue;
                        }
                        int age = youngest + (int)Math.Floor(U("age", P("c" + c)) * (eldest - youngest + 1));
                        Gen child = Person(age, Female("c" + c), "c" + c);
                        Tie(a, grand ? Kin.Grandparent : Kin.Parent, child);
                        if (partner is not null)
                            Tie(partner, grand ? Kin.Grandparent : blended ? Kin.Stepparent : Kin.Parent, child);
                        children.Add(child);
                    }
                    for (int i = 0; i < children.Count; i++)
                        for (int j = i + 1; j < children.Count; j++)
                            Tie(children[i], Kin.Sibling, children[j]);
                    break;
                }
            }
        }

        /// <summary>Kin in other households, only toward earlier slots (town spec 4.3, step 6): about
        /// 30% of elder households have an adult child's household in town, and about 20% of adults
        /// a sibling in another household.</summary>
        private void KinAcross(List<Household> houses, int k)
        {
            var earlier = _people.Where(p => p.Slot < k).ToList();
            foreach (Household house in houses)
            {
                // An elder household (a widow, or a couple) with a grown child elsewhere: the child is
                // every elder's in it, fits all their ages (22-45 at the birth), and has no parents yet.
                var elders = house.Members.Where(m => m.Age >= 63).ToList();
                if (elders.Count == house.Members.Count && elders.Any(m => m.Age >= 65) && U("elder-child", house.Surname) < 0.3)
                {
                    Gen? child = earlier.Where(p => p.Age >= 25 && elders.All(e => p.Age <= e.Age - 22 && p.Age >= e.Age - 45)
                            && !p.Family.Values.Any(x => x is Kin.Parent or Kin.Stepparent))
                        .OrderBy(p => U("pick-child", house.Surname, p.Name)).FirstOrDefault();
                    if (child is not null)
                        foreach (Gen e in elders)
                        {
                            child.Family[e.Name] = Kin.Parent;
                            e.Family[child.Name] = Kin.Child;
                        }
                }
                foreach (Gen g in house.Members)
                {
                    string key = g.Name;
                    if (g.Age >= 21 && g.Age < 65 && U("sibling", key) < 0.2)
                    {
                        Gen? sib = earlier.Where(p => p.Age >= 18 && Math.Abs(p.Age - g.Age) <= 12 && p.Household != g.Household)
                            .OrderBy(p => U("pick-sibling", key, p.Name)).FirstOrDefault();
                        if (sib is not null)
                        {
                            sib.Family[g.Name] = Kin.Sibling;
                            g.Family[sib.Name] = Kin.Sibling;
                        }
                    }
                }
            }
            _people.AddRange(houses.SelectMany(h => h.Members));
        }

        // ---- character ---------------------------------------------------------------------

        private static readonly Func<Temperament, double>[] TraitOf =
        {
            t => t.Chattiness, t => t.Boldness, t => t.Understanding, t => t.SelfRegard, t => t.Sensitivity, t => t.Retention, t => t.Expression,
        };

        private static (double Mean, double Sd)[] Spread(IReadOnlyList<Villager> cast)
            => TraitOf.Select(f =>
            {
                double mean = cast.Average(v => f(v.Temperament));
                double sd = Math.Sqrt(cast.Average(v => Math.Pow(f(v.Temperament) - mean, 2)));
                return (mean, Math.Max(0.05, sd));
            }).ToArray();

        private static Temperament Make(double[] x) => new(x[0], x[1], x[2], x[3], x[4], x[5], x[6]);

        /// <summary>The cast's trait covariance (population), with a small ridge so it factors.</summary>
        private static double[,] Covariance(IReadOnlyList<Villager> cast)
        {
            int n = TraitOf.Length;
            var mean = TraitOf.Select(f => cast.Average(v => f(v.Temperament))).ToArray();
            var c = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    c[i, j] = cast.Average(v => (TraitOf[i](v.Temperament) - mean[i]) * (TraitOf[j](v.Temperament) - mean[j])) + (i == j ? 1e-4 : 0);
            return c;
        }

        /// <summary>A lower-triangular L with L Lᵀ = the matrix (Cholesky).</summary>
        private static double[,] Cholesky(double[,] a)
        {
            int n = a.GetLength(0);
            var l = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j <= i; j++)
                {
                    double sum = a[i, j];
                    for (int k = 0; k < j; k++)
                        sum -= l[i, k] * l[j, k];
                    l[i, j] = i == j ? Math.Sqrt(Math.Max(sum, 1e-9)) : sum / l[j, j];
                }
            return l;
        }

        /// <summary>The cards a generated person may take after, by life stage (teens take after the
        /// young adults).</summary>
        private IReadOnlyList<Villager> Cards(int age)
        {
            Stage s = Ages.StageOf(age);
            return _core.Cast.Where(v => v.Name != DefaultTown.Newcomer).Where(v => s switch
            {
                Stage.Child => v.Stage == Stage.Child,
                Stage.Teen => v.Age >= 18 && v.Age <= 30,
                Stage.Elder => v.Stage == Stage.Elder || v.Age >= 60,
                _ => age <= 30 ? v.Age >= 18 && v.Age <= 30 : age < 45 ? v.Age > 30 && v.Age < 45 : v.Age >= 45 && v.Age < 65,
            }).OrderBy(v => v.Name, StringComparer.Ordinal).ToList();
        }

        // Cards used in each neighbourhood, by stage: not reused until all have been (town spec 4.3, step 8).
        private readonly Dictionary<(string, string), HashSet<string>> _usedCards = new();

        /// <summary>Temperament, body, birthday and the card they take after (town spec 4.3, step 8):
        /// a card of the same stage, each trait moved by N(0, half the core's spread); one in ten a
        /// wildcard drawn from the core's means and covariance (so a wildcard keeps the cast's links
        /// between traits, such as bold people being less understanding); children from their birth
        /// parents, not a stepparent (0.3 of the parents' mean).</summary>
        private void Character(Gen g, Household house, string code)
        {
            var cards = Cards(g.Age);
            string band = g.Age < 13 ? "child" : g.Age <= 30 ? "young" : g.Age < 45 ? "adult" : g.Age < 65 ? "older" : "elder";
            if (!_usedCards.TryGetValue((code, band), out var used))
                _usedCards[(code, band)] = used = new HashSet<string>();
            var fresh = cards.Where(c => !used.Contains(c.Name)).ToList();
            if (fresh.Count == 0)
            {
                used.Clear();
                fresh = cards.ToList();
            }
            Villager card = fresh.OrderBy(c => U("card", g.Name, c.Name)).First();
            used.Add(card.Name);
            g.Card = card;
            var spread = _spread["core"];
            var parents = house.Members.Where(m => g.Family.TryGetValue(m.Name, out Kin kin) && kin == Kin.Parent && m.Traits is not null).ToList();
            var x = new double[TraitOf.Length];
            bool wild = U("wild", g.Name) < 0.1;
            var z = Enumerable.Range(0, x.Length).Select(i => N("trait", g.Name, i.ToString())).ToArray();
            for (int i = 0; i < x.Length; i++)
            {
                double wildValue = spread[i].Mean;
                for (int k = 0; k <= i; k++)
                    wildValue += _wildFactor[i, k] * z[k];
                double value = g.Age < 13 && parents.Count > 0
                    ? spread[i].Mean + spread[i].Sd * (0.3 * parents.Average(p => (TraitOf[i](p.Traits) - spread[i].Mean) / spread[i].Sd) + 0.977 * z[i])
                    : wild ? wildValue
                    : TraitOf[i](card.Temperament) + 0.5 * spread[i].Sd * z[i];
                x[i] = Math.Round(Math.Clamp(value, 0.02, 0.98), 2);
            }
            g.Traits = Make(x);
            g.Body = new Body(Math.Round(card.Body.MaxEnergy + R(-10, 10, "energy", g.Name)),
                Math.Round(Math.Clamp(card.Body.BedAt + (U("bed", g.Name) - 0.5) * 0.1, 0.02, 0.45), 2));
            int day = R(0, WithdrawalMetrics.Year - 1, "birthday", g.Name);
            g.Birthday = new YearDay(day / Clock.DaysPerSeason, day % Clock.DaysPerSeason + 1);
        }

        // ---- livelihoods -------------------------------------------------------------------

        /// <summary>Jobs (town spec 4.3, step 7): each slot brings its posts, filled by the nearest
        /// suitable adults of its own households in keyed order; children and teens up to 17 go to
        /// lessons in the square; the old retire on a pension.</summary>
        private void Livelihoods(Slot slot, Template t, List<Household> houses)
        {
            var posts = new List<Post>();
            if (slot.Code == "E")
            {
                // A café where the east road meets the square.
                var cafe = new Location("Cafe", false, new[] { "............", "............", "..+++++++...", "............", "............", "............", "............", "............" });
                AddPlace(cafe);
                AddLink(new Link(slot.Label.Split(' ')[0] + "Road", new Tile(4, 0), "Cafe", new Tile(6, 7)));
                _publicPlaces.Add("Cafe");
                posts.Add(new Post("Cafe", "cafe-owner", Clock.At(6), Clock.At(14), new[] { 6 }, 1.1, 450, null, Keeper: true));
                posts.Add(new Post("Cafe", "cafe-staff", Clock.At(6), Clock.At(14), new[] { 0 }, 1.1, 350, null));
                posts.Add(new Post("Store", "clerk", Clock.At(9), Clock.At(17), new[] { 2 }, 1.0, 350, "SeedShop"));
                posts.Add(new Post("Saloon", "bar-staff", Clock.At(16), Clock.At(24), new[] { 1 }, 1.1, 350, "Saloon"));
                posts.Add(new Post("Square", "town-clerk", Clock.At(9), Clock.At(17), new[] { 5, 6 }, 0.9, 350, Simulation.Town));
            }
            else
            {
                // A workshop at the lane's far end.
                var shop = new Location("Workshop", false, Enumerable.Range(0, 10).Select(y => y == 4 ? "..++++++++...." : "..............").ToList());
                AddPlace(shop);
                AddLink(new Link(slot.Name, new Tile(t.Width - 1, t.Height / 2), "Workshop", new Tile(0, 5))); // at the street's east end
                posts.Add(new Post("Workshop", "workshop-owner", Clock.At(9), Clock.At(17), new[] { 6 }, 1.2, 700, null, Keeper: true));
                posts.Add(new Post("Workshop", "workshop-staff", Clock.At(9), Clock.At(17), new[] { 5, 6 }, 1.2, 450, null));
                posts.Add(new Post("Home:Clinic", "nurse", Clock.At(9), Clock.At(15), new[] { 5, 6 }, 1.0, 500, null)); // the county pays
                posts.Add(new Post("Mart", "mart-staff", Clock.At(9), Clock.At(17), new[] { 3 }, 1.1, 350, null));
                posts.Add(new Post("Blacksmith", "apprentice", Clock.At(9), Clock.At(16), new[] { 4 }, 1.3, 350, null)); // from the orders out of town
            }
            var free = houses.SelectMany(h => h.Members).Where(g => g.Age >= 18 && g.Age < 65).ToList();
            foreach (Post post in posts)
            {
                if (free.Count == 0)
                    break;
                Location place = Place(post.Place);
                Tile door = DoorOf(post.Place);
                // Staff stand near the keeper's own spot in a core place (behind the bar, not in the back
                // room), near the middle of a core place with no keeper, and near the door of a new one.
                Tile? keeperSpot = _core.Authority.Keepers.TryGetValue(post.Place, out string? keeper)
                    ? _core.Cast.FirstOrDefault(v => v.Name == keeper)?.Job is { } kj && kj.Place == post.Place ? kj.Spot : null
                    : null;
                Tile centre = keeperSpot ?? (_core.Places.Any(p => p.Name == post.Place) ? new Tile(place.Width / 2, place.Height / 2) : door);
                var pick = free.Select(g => (G: g, D: Dist(g.Home, new Tile(2, 2), post.Place, door)))
                    .Where(x => x.D >= 0)
                    .OrderBy(x => x.D + 30 * U("hire", post.Role, x.G.Name)).FirstOrDefault();
                if (pick.G is null)
                    continue;
                Gen g = pick.G;
                free.Remove(g);
                Tile spot = FreeSpot(post.Place, place, centre, keeperSpot is null ? 6 : 4, "job", g.Name);
                g.Job = new Job(post.Place, spot, post.Start, post.End, post.DaysOff, post.Effort, pick.D / 2 + 10);
                g.Workplace = post.Place;
                _incomes.Add((g.Name, post.Wage, post.PaidBy));
                if (post.Keeper)
                    _keepers[post.Place] = g.Name;
            }
            foreach (Gen g in houses.SelectMany(h => h.Members))
            {
                if (g.Age < 18)
                {
                    Tile seat = FreeSpot("Square", Place("Square"), new Tile(12, 7), 4, "lessons", g.Name);
                    g.Job = new Job("Square", seat, Clock.At(10), Clock.At(14), new[] { 5, 6 }, 0.8);
                    g.Workplace = "Square"; // classmates
                    if (g.Age >= 13)
                        _allowances[g.Name] = 30 + 10 * R(0, 2, "allowance", g.Name);
                    else
                        _allowances[g.Name] = 10;
                }
                else if (g.Age >= 65 && g.Job is null)
                    _incomes.Add((g.Name, 400 + 50 * R(0, 4, "pension", g.Name), null));
                else if (g.Age < 20 && g.Job is null)
                    _allowances[g.Name] = 30 + 10 * R(0, 2, "allowance", g.Name); // still at home, not yet working: as a teen
                _wants[g.Name] = g.Age < 13 ? (20, 80) : g.Age < 20 ? (50, 200) : (50, 300);
            }
            // Every household lives on something: one with no wage and no pension has a member who
            // earns from outside (art, freelance work, a stall in the next town), as Leah and Sebastian
            // do, enough for its size (200 + 100 a member + up to 200).
            var earners = _incomes.Select(i => i.Who).ToHashSet(StringComparer.Ordinal);
            foreach (Household house in houses)
            {
                if (house.Members.Any(m => earners.Contains(m.Name)))
                    continue;
                Gen? earner = house.Members.Where(m => m.Age >= 18).OrderByDescending(m => m.Age < 65).ThenByDescending(m => m.Age).FirstOrDefault();
                if (earner is not null)
                    _incomes.Add((earner.Name, 200 + 100 * house.Members.Count + 50 * R(0, 4, "outside", earner.Name), null));
            }
        }

        private Tile DoorOf(string place)
        {
            foreach (Link l in _links)
            {
                if (l.A == place) return l.DoorA;
                if (l.B == place) return l.DoorB;
            }
            return new Tile(0, 0);
        }

        private int Dist(string fromPlace, Tile from, string toPlace, Tile to) => _router.Tiles(fromPlace, from, toPlace, to);

        /// <summary>An open tile in a place within a radius of a centre, not a door, and at least 2
        /// tiles from every spot already taken there; keyed. Falls back to the centre's nearest open tile.</summary>
        private Tile FreeSpot(string placeName, Location place, Tile centre, int radius, params string[] key)
        {
            var doors = _links.SelectMany(l => new[] { (l.A, l.DoorA), (l.B, l.DoorB) }).Where(d => d.Item1 == placeName).Select(d => d.Item2).ToHashSet();
            var taken = _spots.GetValueOrDefault(placeName) ?? new List<Tile>();
            List<Tile> Around(int r, int spacing)
            {
                var found = new List<Tile>();
                for (int y = Math.Max(0, centre.Y - r); y <= Math.Min(place.Height - 1, centre.Y + r); y++)
                    for (int x = Math.Max(0, centre.X - r); x <= Math.Min(place.Width - 1, centre.X + r); x++)
                    {
                        var t = new Tile(x, y);
                        if (place.Walkable(t) && !doors.Contains(t) && taken.All(s => s.Chebyshev(t) >= spacing))
                            found.Add(t);
                    }
                return found;
            }
            // Within the radius and 2 tiles from every spot taken; failing that, the nearest ring out
            // that has room, then anywhere 1 tile from the others, then anywhere at all.
            int whole = Math.Max(place.Width, place.Height);
            var candidates = Around(radius, 2);
            for (int r = radius + 2; candidates.Count == 0 && r <= whole; r += 2)
                candidates = Around(r, 2);
            if (candidates.Count == 0)
                candidates = Around(whole, 1);
            if (candidates.Count == 0)
                candidates = Around(whole, 0);
            Tile pick = candidates.OrderBy(t => U(key.Append("spot").Append($"{t.X},{t.Y}").ToArray())).First();
            Take(placeName, pick);
            return pick;
        }

        // ---- haunts, acts, friends, tensions -----------------------------------------------

        /// <summary>Free-time haunts (town spec 4.3, step 9): one to four for an adult, one or two for a
        /// child, each a zone chosen by weight x exp(-tiles from home / 40), doubled where the card they
        /// take after has a haunt of that place; the shy favour quiet zones. Some keep a front step.</summary>
        private void Haunts(Gen g, Slot slot, Template t, int k)
        {
            var zones = new List<(string Place, Tile Centre, int Radius, double Weight, bool Quiet, int From, int To)>
            {
                ("Square", new Tile(15, 13), 6, 3, false, Clock.At(10), Clock.At(18)),
                ("Beach", new Tile(15, 6), 8, 2, true, Clock.At(9), Clock.At(18)),
                ("Store", new Tile(8, 2), 3, 1, false, Clock.At(9), Clock.At(17)),
                ("Mart", new Tile(9, 8), 3, 1, false, Clock.At(9), Clock.At(17)),
                ("ClinicYard", new Tile(4, 5), 3, 0.5, true, Clock.At(10), Clock.At(17)),
            };
            if (g.Age >= 18)
                zones.Add(("Saloon", new Tile(10, 3), 4, 2, false, Clock.At(18), Clock.At(23)));
            if (_publicPlaces.Contains("Cafe"))
                zones.Add(("Cafe", new Tile(6, 5), 3, 2, false, Clock.At(7), Clock.At(13)));
            foreach (var (hood, linger, radius) in _greens)
                zones.Add((hood, linger, radius + 1, hood == g.Hood ? 3 : 1, true, Clock.At(16), Clock.At(20)));
            if (g.Age < 18)
                zones = zones.Where(z => z.Place is "Square" or "Beach" || z.Place == g.Hood).Select(z => z with { From = Clock.At(14), To = Clock.At(18) }).ToList();
            bool shy = g.Traits.Chattiness < 0.4;
            var weighted = zones.Select(z =>
            {
                int d = Dist(g.Home, new Tile(2, 2), z.Place, z.Centre);
                double w = z.Weight * Math.Exp(-(d < 0 ? 200 : d) / 40.0);
                if (g.Card.Haunts.Any(h => h.Place == z.Place))
                    w *= 2;
                if (shy)
                    w *= z.Quiet ? 1.5 : 0.5;
                return (Z: z, W: w);
            }).Where(x => x.W > 0).ToList();
            int count = g.Age < 18 ? R(1, 2, "haunts", g.Name) : R(1, 4, "haunts", g.Name);
            for (int i = 0; i < count && weighted.Count > 0; i++)
            {
                double r = U("zone", g.Name, i.ToString()) * weighted.Sum(x => x.W);
                var pick = weighted[^1];
                foreach (var x in weighted)
                {
                    r -= x.W;
                    if (r < 0) { pick = x; break; }
                }
                weighted.Remove(pick);
                var z = pick.Z;
                Haunt? like = g.Card.Haunts.FirstOrDefault(h => h.Place == z.Place);
                int from = like?.From ?? z.From, to = like?.To ?? z.To;
                if (g.Age < 18)
                    (from, to) = (Clock.At(14), Clock.At(18));
                Tile spot = FreeSpot(z.Place, Place(z.Place), z.Centre, z.Radius, "haunt", g.Name, i.ToString());
                g.Haunts.Add(new Haunt(z.Place, spot, from, to, 1 + 0.5 * R(0, 4, "weight", g.Name, i.ToString())));
            }
            // The front step (town spec 2.6): most elders and some adults sit out in the evening.
            if (g.Age >= 18 && U("step", g.Name) < (g.Age >= 65 ? 0.8 : 0.4))
                g.Haunts.Add(new Haunt(g.Hood, FreeSpot(g.Hood, Place(g.Hood), g.Step, 1, "step", g.Name), Clock.At(16), Clock.At(19), 1));
            if (g.Haunts.Count == 0)
                g.Haunts.Add(new Haunt(g.Hood, FreeSpot(g.Hood, Place(g.Hood), t.Linger, t.LingerRadius + 2, "fallback", g.Name), Clock.At(9), Clock.At(20), 1));
        }

        private static readonly string[] Vices = { "Stole", "RummagedInBin", "DrunkScene" };

        /// <summary>Acts (town spec 4.3, step 12): the card's everyday acts at 0.7-1.3 of its weights;
        /// vices drawn on their own among adults at the core's rates; squabbles for a child with a
        /// child sibling.</summary>
        private void Acts(Gen g, Household house, string code)
        {
            foreach (var (kind, w) in g.Card.Acts.OrderBy(a => a.Key, StringComparer.Ordinal))
                if (!Vices.Contains(kind) && kind != "Squabbled")
                    g.Acts[kind] = Math.Round(w * (0.7 + 0.6 * U("act", g.Name, kind)), 2);
            if (g.Age >= 18)
            {
                if (U("vice", g.Name, "Stole") < 0.2) g.Acts["Stole"] = 0.2;
                if (U("vice", g.Name, "RummagedInBin") < 0.12) g.Acts["RummagedInBin"] = 0.3;
                if (U("vice", g.Name, "DrunkScene") < 0.08) g.Acts["DrunkScene"] = 1.0;
            }
            if (g.Age < 13 && house.Members.Any(m => m != g && m.Age < 13 && g.Family.GetValueOrDefault(m.Name) == Kin.Sibling))
                g.Acts["Squabbled"] = 1.0;
        }

        /// <summary>Friends (town spec 4.3, step 10): zero to three, by likeness (life stage, the same
        /// neighbourhood, the same workplace, chattiness), from their own neighbourhood, earlier ones and
        /// the core; seven in ten returned, inside a neighbourhood only (a friendship toward someone
        /// earlier lives on the new card, which is enough: either card counts).</summary>
        private void Friends(Gen g, int k)
        {
            if (g.Age < 5)
                return;
            int want = R(0, 3, "friends", g.Name);
            var candidates = _people.Where(p => p != g && p.Slot <= k && p.Household != g.Household && !g.Family.ContainsKey(p.Name))
                .Select(p => (Name: p.Name, Gen: (Gen?)p, Stage: p.Stage, Work: p.Workplace, Chat: p.Traits.Chattiness, Same: p.Hood == g.Hood))
                .Concat(_core.Cast.Where(v => v.Name != DefaultTown.Newcomer)
                    .Select(v => (Name: v.Name, Gen: (Gen?)null, Stage: v.Stage, Work: v.Job?.Place, Chat: v.Temperament.Chattiness, Same: false)))
                .ToList();
            for (int i = 0; i < want && candidates.Count > 0; i++)
            {
                var weighted = candidates.Select(c => (C: c, W: (c.Stage == g.Stage ? 2.0 : 0.5) * (c.Same ? 3.0 : 1.0)
                    * (c.Work is not null && c.Work == g.Workplace ? 2.0 : 1.0) * (1.2 - Math.Abs(c.Chat - g.Traits.Chattiness)))).ToList();
                double r = U("friend", g.Name, i.ToString()) * weighted.Sum(x => x.W);
                var pick = weighted[^1].C;
                foreach (var x in weighted)
                {
                    r -= x.W;
                    if (r < 0) { pick = x.C; break; }
                }
                candidates.RemoveAll(c => c.Name == pick.Name);
                if (!g.Friends.Contains(pick.Name))
                    g.Friends.Add(pick.Name);
                if (pick.Gen is { } other && other.Hood == g.Hood && !other.Friends.Contains(g.Name) && U("returned", g.Name, other.Name) < 0.7)
                    other.Friends.Add(g.Name);
            }
        }

        /// <summary>Starting tensions from the core's sparks (town spec 4.3, step 10): rival keepers
        /// both ways, a stepchild toward a stepparent (one in two), a young adult living with a strict
        /// parent (one in four).</summary>
        private void Tensions(List<Household> houses, string code)
        {
            foreach (var (place, rival) in new[] { ("Cafe", "Gus"), ("Workshop", "Robin") })
                if (_keepers.TryGetValue(place, out string? keeper) && houses.Any(h => h.Members.Any(m => m.Name == keeper)))
                {
                    _tensions[(keeper, rival)] = -DefaultTown.TensionDepth;
                    _tensions[(rival, keeper)] = -DefaultTown.TensionDepth;
                }
            foreach (Household house in houses)
                foreach (Gen g in house.Members)
                {
                    foreach (var (other, kin) in g.Family.OrderBy(f => f.Key, StringComparer.Ordinal))
                    {
                        if (kin == Kin.Stepparent && U("step-tension", g.Name) < 0.5)
                            _tensions[(g.Name, other)] = -DefaultTown.TensionDepth;
                        if (kin == Kin.Parent && g.Age >= 18 && g.Age <= 29 && house.Members.Any(m => m.Name == other) && U("strict", g.Name, other) < 0.25)
                            _tensions[(g.Name, other)] = -DefaultTown.TensionDepth;
                    }
                }
        }

        // ---- the whole town ----------------------------------------------------------------

        public TownData Finish()
        {
            var cast = _core.Cast.Concat(_people.Select(g => new Villager(g.Name, g.Household, g.Kind, g.Traits, g.Body, g.Job,
                g.Haunts.ToList(), new Dictionary<string, double>(g.Acts), g.Friends.ToList(), g.Age,
                new Dictionary<string, Kin>(g.Family), g.Birthday))).ToList();
            int n = cast.Count;
            double scale = _spec.PerCapita * n / 26.0;

            // The core's hubs keep place, time, radius and weight; a grown town adds limits and locals
            // (town spec 2.5). Each neighbourhood has its evening on the green.
            var coreHouseholds = _core.Cast.Select(v => v.Household).Distinct().OrderBy(h => h, StringComparer.Ordinal).ToList();
            var hubs = _core.Gatherings.Select(g => g.Name switch
            {
                "Noon" => g with { Capacity = 25, Local = coreHouseholds, Visitors = 0.25 },
                "Evening" => g with { Capacity = 30 },
                _ => g,
            }).ToList();
            foreach (var (hood, linger, radius) in _greens)
                hubs.Add(new Gathering("Green:" + hood, hood, linger, radius, Clock.At(17), Clock.At(20), Array.Empty<int>(), 2,
                    Capacity: 15, Local: _households.Where(h => h.Hood == hood).Select(h => h.Surname).OrderBy(s => s, StringComparer.Ordinal).ToList(), Visitors: 0.1));

            var acts = _core.Acts.Select(a => a with
            {
                PerDay = a.PerDay * scale,
                Allowed = a.Name == "RummagedInBin" ? a.Allowed.Concat(_greens.Select(x => x.Hood)).ToList() : a.Allowed,
            }).ToList();

            AuthorityOptions core = _core.Authority;
            var authority = new AuthorityOptions
            {
                Mayor = core.Mayor, Constable = core.Constable, ElectConstable = core.ElectConstable, VoteMinute = core.VoteMinute,
                StandAt = core.StandAt, PatrolHours = core.PatrolHours, PatrolStopMinutes = core.PatrolStopMinutes,
                ReportBase = core.ReportBase, ReportPerBoldness = core.ReportPerBoldness, VerdictWeight = core.VerdictWeight,
                VerdictLead = core.VerdictLead, HearsayWeight = core.HearsayWeight, NearbyWeight = core.NearbyWeight,
                AlibiWeight = core.AlibiWeight, ConfessBase = core.ConfessBase, ConfessPerTimidity = core.ConfessPerTimidity,
                SwayChance = core.SwayChance, SwayCloseAt = core.SwayCloseAt, DetainMinutes = core.DetainMinutes,
                LockupPlace = core.LockupPlace, LockupSpot = core.LockupSpot, ServicePlace = core.ServicePlace, ServiceSpot = core.ServiceSpot,
                Record = core.Record,
                Keepers = core.Keepers.Concat(_keepers).GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.Last().Value),
                Patrol = core.Patrol.Concat(_patrol).GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.Last().Value),
            };

            Economy e = _core.Economy!;
            var economy = e with
            {
                StartPurse = e.StartPurse.Concat(_purses).ToDictionary(x => x.Key, x => x.Value),
                Incomes = e.Incomes.Concat(_incomes).ToList(),
                Allowances = e.Allowances.Concat(_allowances).ToDictionary(x => x.Key, x => x.Value),
                GroceriesAt = e.GroceriesAt.Concat(_groceries).ToDictionary(x => x.Key, x => x.Value),
                Wants = e.Wants.Concat(_wants).ToDictionary(x => x.Key, x => x.Value),
                TownStipend = e.TownStipend * n / 26.0,
                TownStart = e.TownStart * n / 26.0,
            };

            FeelingOptions feelings = DefaultTown.Feelings();
            feelings.Start = _core.Feelings.Start.Concat(_tensions).GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.Last().Value);

            GossipOptions gossip = new() { TellsPerDay = 2 };

            return _core with
            {
                Cast = cast, Places = _places.ToList(), Links = _links.ToList(), Gatherings = hubs, Acts = acts,
                Feelings = feelings, Authority = authority, Economy = economy, Gossip = gossip,
                Familiarity = Seeds(cast, authority),
            };
        }

        /// <summary>Familiarity to start from (town spec 4.3, step 11) for every pair with a generated
        /// person, the largest that applies: kin in another household 0.6, coworkers and classmates 0.4,
        /// the same neighbourhood 0.25, a public figure (the mayor, a keeper, the doctor, the teacher)
        /// 0.2, anyone else the stranger's value. Only values that differ from the engine's own seeds
        /// are stored, and core pairs keep theirs.</summary>
        private IReadOnlyList<(string, string, double)> Seeds(IReadOnlyList<Villager> cast, AuthorityOptions authority)
        {
            var generated = _people.Select(g => g.Name).ToHashSet(StringComparer.Ordinal);
            var publicFigures = authority.Keepers.Values.Append(DefaultTown.Mayor).Append("Harvey").Append("Penny").ToHashSet(StringComparer.Ordinal);
            var hoodOf = _people.ToDictionary(g => g.Name, g => g.Hood);
            var byName = cast.ToDictionary(v => v.Name);
            var seeds = new List<(string, string, double)>();
            var names = cast.Select(v => v.Name).OrderBy(x => x, StringComparer.Ordinal).ToList();
            for (int i = 0; i < names.Count; i++)
                for (int j = i + 1; j < names.Count; j++)
                {
                    string a = names[i], b = names[j];
                    if (!generated.Contains(a) && !generated.Contains(b))
                        continue;
                    if (a == DefaultTown.Newcomer || b == DefaultTown.Newcomer)
                        continue; // nobody knows the newcomer
                    Villager va = byName[a], vb = byName[b];
                    double engine = va.Household == vb.Household ? 0.8 : va.Friends.Contains(b) || vb.Friends.Contains(a) ? 0.5 : 0.25;
                    if (engine >= 0.5)
                        continue; // housemates and friends keep the engine's seeds
                    double value = _spec.StrangerFamiliarity;
                    if (va.KinOf(b) is not null || vb.KinOf(a) is not null) value = Math.Max(value, 0.6);
                    // Coworkers, or classmates: a child at lessons in the square isn't a coworker of the adults who work there.
                    if (va.Job is { } ja && vb.Job is { } jb && ja.Place == jb.Place && va.Age >= 18 == vb.Age >= 18) value = Math.Max(value, 0.4);
                    if (hoodOf.TryGetValue(a, out string? ha) && hoodOf.TryGetValue(b, out string? hb) && ha == hb) value = Math.Max(value, 0.25);
                    if (publicFigures.Contains(a) || publicFigures.Contains(b)) value = Math.Max(value, 0.2);
                    if (value != engine)
                        seeds.Add((a, b, value));
                }
            return seeds;
        }
    }
}
