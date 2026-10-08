namespace UnderGlass.Sim.Generation;

/// <summary>
/// Names for generated people (town spec 4.3, step 5): common English given names and surnames,
/// one ASCII token each, drawn without replacement so every name in a town is unique. None is a
/// Stardew name, a place, an act or a word the engine reserves. The lists are common names written
/// out for this prototype (no list was copied; licence VERIFY before any release).
/// </summary>
public static class Names
{
    public static readonly IReadOnlyList<string> Women = new[]
    {
        "Ada", "Agnes", "Alice", "Alma", "Amber", "Amelia", "Andrea", "Angela", "Anita", "Anna", "April", "Audrey",
        "Beatrice", "Bella", "Bernice", "Beth", "Bonnie", "Brenda", "Bridget", "Camille", "Carla", "Carmen", "Cecily",
        "Celia", "Clara", "Claudia", "Cora", "Daisy", "Daphne", "Delia", "Denise", "Diana", "Dora", "Edith", "Edna",
        "Eileen", "Elaine", "Eleanor", "Elena", "Eliza", "Ella", "Elsie", "Erin", "Esther", "Eva", "Faith", "Fern",
        "Fiona", "Flora", "Frances", "Freya", "Gemma", "Georgia", "Gloria", "Grace", "Greta", "Hannah", "Hazel",
        "Heidi", "Helen", "Hilda", "Holly", "Ida", "Imogen", "Irene", "Iris", "Isla", "Ivy", "Jane", "Janet", "Jean",
        "Joan", "Josie", "Joy", "Judith", "Julia", "June", "Karen", "Kate", "Kathleen", "Laura", "Lena", "Libby",
        "Lila", "Lily", "Lois", "Lorna", "Lucy", "Lydia", "Mabel", "Maggie", "Maisie", "Margot", "Marian", "Martha",
        "Matilda", "Maud", "May", "Megan", "Mildred", "Millie", "Miriam", "Molly", "Mona", "Nadia", "Nancy", "Naomi",
        "Nell", "Nina", "Nora", "Olive", "Olivia", "Opal", "Paula", "Pearl", "Phoebe", "Polly", "Rachel", "Rita",
        "Rosa", "Rose", "Ruby", "Ruth", "Sadie", "Sally", "Sarah", "Sophie", "Stella", "Susan", "Sylvia", "Tessa",
        "Thea", "Una", "Vera", "Violet", "Wendy", "Winnie", "Yvonne", "Zoe",
    };

    public static readonly IReadOnlyList<string> Men = new[]
    {
        "Aaron", "Adam", "Albert", "Alfred", "Allan", "Amos", "Andrew", "Angus", "Arthur", "Barney", "Basil", "Ben",
        "Bernard", "Bill", "Bruce", "Calvin", "Carl", "Cecil", "Charles", "Chester", "Colin", "Conrad", "Dale",
        "Daniel", "David", "Dennis", "Derek", "Dominic", "Donald", "Douglas", "Duncan", "Earl", "Edgar", "Edmund",
        "Edward", "Edwin", "Eli", "Ernest", "Eugene", "Felix", "Floyd", "Francis", "Frank", "Fred", "Gareth", "Gavin",
        "Gerald", "Gilbert", "Glen", "Gordon", "Graham", "Guy", "Hank", "Harold", "Henry", "Herbert", "Homer", "Howard",
        "Hugh", "Ian", "Isaac", "Ivan", "Jack", "Jacob", "James", "Jasper", "Jerome", "Jesse", "Joel", "John", "Jonah",
        "Joseph", "Julian", "Keith", "Kenneth", "Lionel", "Lloyd", "Louis", "Luke", "Malcolm", "Martin", "Matthew",
        "Max", "Miles", "Milo", "Murray", "Nathan", "Neil", "Ned", "Nigel", "Noah", "Norman", "Oliver", "Oscar",
        "Owen", "Patrick", "Paul", "Percy", "Peter", "Philip", "Ralph", "Raymond", "Reggie", "Rex", "Roger", "Roland",
        "Ronald", "Ross", "Rufus", "Rupert", "Russell", "Samuel", "Silas", "Simon", "Stanley", "Stephen", "Stuart",
        "Theo", "Thomas", "Toby", "Tom", "Victor", "Wallace", "Walter", "Warren", "Wesley", "Wilfred", "Xavier",
    };

    public static readonly IReadOnlyList<string> Surnames = new[]
    {
        "Abbott", "Ainsley", "Archer", "Ashby", "Bailey", "Baker", "Barlow", "Barnes", "Bates", "Beck", "Bell",
        "Bennett", "Birch", "Blake", "Bond", "Booth", "Bowen", "Bradley", "Brook", "Bryant", "Burke", "Butler",
        "Carter", "Chandler", "Chapman", "Clarke", "Cole", "Collins", "Cooper", "Cross", "Dale", "Dalton", "Davies",
        "Dawson", "Dixon", "Doyle", "Drake", "Dunn", "Eaton", "Elliston", "Ellis", "Emery", "Farmer", "Fenwick",
        "Fisher", "Fletcher", "Ford", "Foster", "Fowler", "Fox", "Frost", "Fuller", "Gardner", "Garrett", "Gibbs",
        "Goodwin", "Graves", "Gray", "Hale", "Hall", "Hammond", "Harding", "Harper", "Hart", "Hayes", "Hill",
        "Hobbs", "Holland", "Holt", "Hood", "Hope", "Howell", "Hughes", "Hunt", "Ingram", "Jarvis", "Jenkins",
        "Keane", "Kemp", "Knight", "Lamb", "Lane", "Lawson", "Lee", "Lloyd", "Lowe", "Lucas", "Lyons", "Mann",
        "Marsh", "Mason", "Meadows", "Miller", "Moore", "Morgan", "Moss", "Murphy", "Nash", "Newman", "Norris",
        "Oakley", "Osborne", "Owens", "Page", "Palmer", "Parker", "Parsons", "Payne", "Pearce", "Perry", "Pike",
        "Porter", "Potter", "Price", "Quinn", "Reed", "Reeves", "Rhodes", "Riley", "Rowe", "Russell", "Ryan",
        "Sawyer", "Shaw", "Short", "Sims", "Slater", "Spencer", "Stone", "Swift", "Taylor", "Thorne", "Tucker",
        "Turner", "Vaughan", "Wade", "Walsh", "Ward", "Warner", "Watts", "Webb", "Wells", "West", "Wheeler",
        "Whitaker", "Wilde", "Winter", "Wood", "Wright", "Wyatt", "Yates", "York", "Young",
    };

    /// <summary>Names a generated person or household may never take: every Stardew name the cast
    /// uses or the game has (recalled, VERIFY), the engine's own words, and the places' and acts'.</summary>
    public static readonly IReadOnlySet<string> Reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Abigail", "Alex", "Caroline", "Clint", "Demetrius", "Elliott", "Emily", "Evelyn", "George", "Gus", "Haley",
        "Harvey", "Jas", "Jodi", "Kent", "Krobus", "Leah", "Lewis", "Linus", "Marnie", "Maru", "Pam", "Penny",
        "Pierre", "Robin", "Sam", "Sandy", "Sebastian", "Shane", "Vincent", "Willy", "Wizard", "Morris", "Gunther",
        "Marlon", "Leo", "Birdie", "Gil", "Qi", "Rasmodius", "Joja", "Mullner", "Dwarf", "Bouncer", "Grandpa",
        "Newcomer", "Town", "someone", "Anyone", "Square", "Saloon", "Store", "Mart", "Beach", "Farm", "Clinic",
        "Cottage", "Manor", "Ranch", "Trailer", "Blacksmith", "Tower", "Tent", "Cafe", "Workshop", "Green", "Lane",
    };
}
