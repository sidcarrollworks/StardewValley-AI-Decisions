using NpcMotives;

namespace NpcLive;

/// <summary>
/// One villager's speech-bubble lines, by feeling (docs/spec/text.md, "Bubbles"). Friendly sets:
/// a greeting, missing the player, news, thanks, worry. Hostile sets: hurt, jealous. An empty set
/// falls back to the plain lines in <see cref="LivePlanner"/>. "{player}" is the farmer's name.
/// Every line is short (it floats over a head for three seconds), written by Claude in the
/// character's manner, never copied from the game's dialogue, and Sid's to edit.
/// </summary>
public sealed record BubbleVoice(
    string[] Greeting,
    string[] MissingYou,
    string[] News,
    string[] Grateful,
    string[] Worried,
    string[] Hurt,
    string[] Jealous)
{
    /// <summary>The set for a feeling, or an empty one.</summary>
    public string[] For(Motive motive, bool hostile) => hostile
        ? motive switch { Motive.Hurt => Hurt, Motive.Jealous => Jealous, _ => Array.Empty<string>() }
        : motive switch
        {
            Motive.Greeting => Greeting,
            Motive.MissingYou => MissingYou,
            Motive.News => News,
            Motive.Grateful => Grateful,
            Motive.Worried => Worried,
            _ => Array.Empty<string>(),
        };
}

/// <summary>The bubble voices of the 34 vanilla villagers. Editable data, not logic.</summary>
public static class BubbleVoices
{
    private static string[] L(params string[] lines) => lines;

    public static readonly IReadOnlyDictionary<string, BubbleVoice> All = new Dictionary<string, BubbleVoice>(StringComparer.OrdinalIgnoreCase)
    {
        ["Abigail"] = new(
            L("Oh hey, {player}!", "Hey! Fancy seeing you.", "Yo, farmer!"),
            L("Where've you been hiding?", "There you are! It's been forever."),
            L("Psst, I've got gossip.", "You'll want to hear this!"),
            L("Thanks again, really!", "You're kind of awesome, you know."),
            L("You okay? You look beat.", "Hey... everything alright?"),
            L("Oh. It's you.", "I'm not talking to you right now."),
            L("Wow. Busy with your new friends?", "Didn't think you'd remember me.")),
        ["Alex"] = new(
            L("Hey, {player}! What's up?", "Yo! Looking good today.", "Hey there!"),
            L("Haven't seen you in a while!", "Where you been, champ?"),
            L("Guess what I heard!", "Got some news for you."),
            L("Thanks, I owe you one!", "You're alright, {player}."),
            L("You doing okay? Need a hand?", "Hey, take it easy, alright?"),
            L("...Whatever.", "Not now, okay?"),
            L("Oh, so you've got time for them.", "Huh. Nice of you to notice me.")),
        ["Caroline"] = new(
            L("Hello, dear!", "Oh, hi {player}!", "Good to see you!"),
            L("There you are! We missed you.", "It's been a while, dear."),
            L("Oh, I have something to tell you!", "You won't believe what I heard."),
            L("Thank you again, sweetie.", "That was so thoughtful of you."),
            L("Are you eating enough, dear?", "You look tired. Rest a little?"),
            L("Hmm. Hello.", "I expected better from you."),
            L("Oh. You're popular lately.", "Some of us noticed, you know.")),
        ["Clint"] = new(
            L("Oh... hi, {player}.", "H-hey there.", "Uh, hello."),
            L("Haven't seen you around...", "Oh! I, uh, wondered where you went."),
            L("Um, I heard something...", "Got a minute? It's... news."),
            L("Th-thanks. Really.", "That meant a lot. Thanks."),
            L("You alright? You look tired.", "Don't, uh, overdo it."),
            L("...Hmph.", "I'd rather not talk."),
            L("Guess I'm not interesting enough.", "...Must be nice.")),
        ["Demetrius"] = new(
            L("Ah, hello {player}.", "Good day!", "Hello there."),
            L("Ah, haven't seen you lately.", "There you are. Been busy?"),
            L("I've made an observation!", "Interesting news, actually."),
            L("Much appreciated, {player}.", "Thank you. Truly."),
            L("Are you getting enough sleep?", "You look worn out. Data agrees."),
            L("Hm. I'm busy.", "I'd rather keep this brief."),
            L("Interesting how you spend your time.", "Noted.")),
        ["Dwarf"] = new(
            L("Oh. You again.", "Hmph. Hello, human.", "Greetings, surface one."),
            L("Haven't seen you down here.", "You were gone a while."),
            L("Heard rumblings in the deep.", "Something to tell you, human."),
            L("Your gift was... acceptable. Thanks.", "Thank you, human."),
            L("Careful in the mines, human.", "You look scraped up."),
            L("Go away.", "Hmph. Humans."),
            L("Off chatting with other humans?", "Hmph.")),
        ["Elliott"] = new(
            L("Ah, {player}! Splendid.", "Good day, my friend!", "What a pleasant surprise!"),
            L("Ah, at last! Where have you been?", "The days were long without you."),
            L("I have a tale for you!", "Gather close, I've news!"),
            L("My heartfelt thanks.", "You are too generous, truly."),
            L("You seem weary, my friend.", "Do rest, {player}. Please."),
            L("Ah. Good day.", "I find myself... wounded."),
            L("I see your attentions lie elsewhere.", "How fickle the heart.")),
        ["Emily"] = new(
            L("Hi, {player}!", "Oh, your aura's bright today!", "Hello, hello!"),
            L("I had a feeling you'd turn up!", "Where've you been? I missed you!"),
            L("Ooh, I have news!", "The spirits told me... well, Gus did."),
            L("Thank you! That was lovely.", "You have such a giving spirit!"),
            L("Your energy feels low today.", "Are you alright? Truly?"),
            L("My aura's a little dark today.", "I need some space."),
            L("Hm. Your energy's elsewhere.", "Oh. I see how it is.")),
        ["Evelyn"] = new(
            L("Hello, dearie!", "Oh, how nice to see you!", "Good morning, sweetheart."),
            L("There you are, dearie!", "I was wondering about you."),
            L("Oh, I heard the funniest thing!", "Dearie, have you heard?"),
            L("Thank you, dearie.", "Such a kind young one."),
            L("Don't work too hard, dear.", "You look tired, sweetheart."),
            L("Oh. Hello, dear.", "I'm a little disappointed."),
            L("You've been busy with others, dear.", "Hm. I see.")),
        ["George"] = new(
            L("Hmph. Hello.", "Oh, it's you.", "Morning."),
            L("Hmph. Where've you been?", "Haven't seen you."),
            L("Heard something. Listen up.", "Got news, if you care."),
            L("...Thanks.", "Hmph. Appreciate it."),
            L("You look terrible. Rest.", "Don't work yourself sick."),
            L("Leave me be.", "Hmph. Go on."),
            L("Hmph. Got new friends, eh?", "Don't mind me.")),
        ["Gus"] = new(
            L("Hey, {player}!", "Good to see you, friend!", "Hello there!"),
            L("Haven't seen you at the saloon!", "There you are, friend!"),
            L("Hey, I heard something!", "Got some news for you!"),
            L("Thanks a bunch!", "You're a good one, {player}."),
            L("You eating right? You look tired.", "Take care of yourself, friend."),
            L("Hm. Hello.", "Not now, friend."),
            L("Busy with everyone else, huh?", "Hm. Haven't seen you around.")),
        ["Haley"] = new(
            L("Oh, hi.", "Hey, {player}.", "Hi! Cute outfit... ish."),
            L("Where have you been?", "Oh! You're back."),
            L("Okay, you have to hear this.", "Ugh, guess what happened."),
            L("Thanks! That was sweet.", "Aww, thanks."),
            L("You look exhausted. Sit down?", "Are you okay? Seriously."),
            L("Ugh. Hi.", "I'm not in the mood."),
            L("Oh, sure, go talk to them.", "Whatever. I didn't care anyway.")),
        ["Harvey"] = new(
            L("Oh, hello {player}.", "Good morning!", "Ah, hello there."),
            L("Ah, you're back. I wondered.", "Haven't seen you at the clinic."),
            L("I, um, heard something.", "May I tell you something?"),
            L("Thank you, really.", "That's very kind of you."),
            L("Are you feeling alright?", "You should rest. Doctor's orders."),
            L("Oh. Um. Hello.", "I'd rather not, right now."),
            L("Oh. You seem... busy.", "I see. Never mind.")),
        ["Jas"] = new(
            L("Hi, {player}!", "Hello!", "Hi hi!"),
            L("Where were you?", "I missed you!"),
            L("Guess what! Guess what!", "I have a secret!"),
            L("Thank you!", "Yay, thanks!"),
            L("Are you sad?", "Are you okay?"),
            L("Hmph!", "I'm not talking to you."),
            L("You play with everyone else!", "Hmph.")),
        ["Jodi"] = new(
            L("Hello, {player}!", "Oh, hi there!", "Morning!"),
            L("Haven't seen you in a while!", "There you are!"),
            L("Oh, I have news!", "You'll never guess what I heard."),
            L("Thank you so much!", "That was so kind."),
            L("You look worn out, hon.", "Take a break, okay?"),
            L("Hm. Hello.", "I'm a little upset with you."),
            L("You've been busy, I see.", "Oh. Hi, I guess.")),
        ["Kent"] = new(
            L("Hey.", "Morning, {player}.", "Hello."),
            L("Haven't seen you around.", "Been a while."),
            L("Heard something you should know.", "Got a minute?"),
            L("Thanks. Really.", "Appreciate it."),
            L("You okay? Get some rest.", "Take care out there."),
            L("Not now.", "...Hm."),
            L("Busy, huh.", "Right.")),
        ["Krobus"] = new(
            L("Hello, friend.", "Oh! A visitor.", "Greetings, {player}."),
            L("I have not seen you in days.", "You returned!"),
            L("I heard whispers in the sewer.", "I have news, friend."),
            L("Thank you. Truly.", "Your kindness is noted."),
            L("You look weary, friend.", "Rest, friend. Please."),
            L("I would like to be alone.", "...Hm."),
            L("You have other friends now.", "I see.")),
        ["Leah"] = new(
            L("Hi, {player}!", "Oh, hello!", "Hey there!"),
            L("There you are! Been a while.", "I was hoping I'd see you."),
            L("Oh, I have news!", "You have to hear this."),
            L("Thank you, really.", "That was so thoughtful."),
            L("You look tired. Take a break?", "Hey, are you alright?"),
            L("Hm. Hi.", "I need a little space."),
            L("Oh. You've been busy, then.", "I see how it is.")),
        ["Leo"] = new(
            L("Hi, {player}!", "Hello, friend!", "Hi!"),
            L("You came back!", "I missed you!"),
            L("I saw something amazing!", "Guess what I found!"),
            L("Thank you, friend!", "Thank you!"),
            L("Are you okay?", "You look sad."),
            L("Hmph.", "I'm upset."),
            L("You have other friends?", "Hm.")),
        ["Lewis"] = new(
            L("Ah, {player}! Good day.", "Hello there!", "Morning, farmer."),
            L("Haven't seen you in town lately.", "Ah, there you are."),
            L("Town news, {player}!", "I have an announcement, of sorts."),
            L("Thank you, {player}.", "Much obliged."),
            L("Don't overwork yourself.", "You look tired. Rest a little."),
            L("Hmm. Hello.", "I'm disappointed, frankly."),
            L("Busy with others, I see.", "Hm.")),
        ["Linus"] = new(
            L("Hello, friend.", "Oh, hello there.", "Good to see you."),
            L("Haven't seen you in a while.", "Ah. You came by."),
            L("The woods told me something.", "I heard news, friend."),
            L("Thank you, friend.", "That warms the heart."),
            L("You look tired, friend.", "Rest by a fire tonight."),
            L("I'd like to be alone.", "...Hm."),
            L("You've been busy in town.", "I see.")),
        ["Marnie"] = new(
            L("Hello, dear!", "Oh, hi {player}!", "Morning!"),
            L("There you are, dear!", "Haven't seen you at the ranch!"),
            L("Oh, have you heard?", "I've got news, dear."),
            L("Thank you, dear!", "So kind of you."),
            L("You look tired, dear.", "Take it easy, alright?"),
            L("Hm. Hello.", "I'm a bit upset with you."),
            L("You've been busy, dear.", "Oh. Hello.")),
        ["Maru"] = new(
            L("Hi, {player}!", "Oh, hey!", "Hello!"),
            L("There you are! Been a while.", "I was wondering where you went."),
            L("I've got news, and data!", "You'll want to hear this."),
            L("Thanks! Really.", "That was nice of you."),
            L("You look tired. Sleep more?", "Hey, are you okay?"),
            L("Hm. Hi.", "I'm a little annoyed, honestly."),
            L("Oh. You've been busy.", "Huh. Okay.")),
        ["Pam"] = new(
            L("Hey, kid.", "Oh, it's you! Hiya.", "Howdy, {player}."),
            L("Where ya been, kid?", "There you are!"),
            L("Heard somethin' you'll like.", "Got news, kid."),
            L("Thanks, kid.", "You're alright, {player}."),
            L("You look beat, kid.", "Take a load off."),
            L("Hmph.", "Not now, kid."),
            L("Too good for me now, huh?", "Hmph.")),
        ["Penny"] = new(
            L("Oh, hello {player}.", "Hi there.", "Good morning!"),
            L("Oh! I wondered where you were.", "It's been a while."),
            L("I heard something...", "May I tell you something?"),
            L("Thank you, truly.", "That was very kind."),
            L("Are you alright?", "You should rest a bit."),
            L("Oh. Hello.", "I'd rather not talk."),
            L("You've been busy, I suppose.", "Oh. I see.")),
        ["Pierre"] = new(
            L("Hello, {player}!", "Ah, my favorite customer!", "Morning!"),
            L("Haven't seen you at the shop!", "There you are!"),
            L("Big news, {player}!", "Have I got news for you!"),
            L("Thanks, {player}!", "Much appreciated!"),
            L("You look tired. Long day?", "Take it easy, friend."),
            L("Hm. Hello.", "I'm busy."),
            L("Shopping elsewhere, are we?", "Hmph.")),
        ["Robin"] = new(
            L("Hey, {player}!", "Morning!", "Hey there!"),
            L("There you are! Been a while.", "Haven't seen you lately!"),
            L("Oh, I've got news!", "Guess what I heard!"),
            L("Thanks a ton!", "You're the best, {player}."),
            L("You look worn out.", "Don't work yourself too hard!"),
            L("Hm. Hi.", "I'm kind of upset."),
            L("You've been busy, huh?", "Oh. Hi.")),
        ["Sam"] = new(
            L("Hey, {player}!", "Yo! What's up?", "Heyyy!"),
            L("Dude, where've you been?", "There you are!"),
            L("Dude, guess what!", "Got news, man!"),
            L("Thanks, dude!", "You rock, {player}!"),
            L("You okay, man?", "Get some rest, dude."),
            L("...Whatever.", "Not now, dude."),
            L("Oh, you hang with them now?", "Cool. Whatever.")),
        ["Sandy"] = new(
            L("Hey, sweetie!", "Hiya, {player}!", "Well, hello!"),
            L("Where've you been, sweetie?", "There you are!"),
            L("Ooh, I've got gossip!", "Desert news, sweetie!"),
            L("Thanks, sweetie!", "You're a doll."),
            L("You look sunburnt, sweetie.", "Stay hydrated, hon!"),
            L("Hmph. Hi.", "Not today, sweetie."),
            L("Busy with town folk, huh?", "Hmph.")),
        ["Sebastian"] = new(
            L("...Hey.", "Oh. Hi, {player}.", "Hey."),
            L("Haven't seen you around.", "...Oh. You're back."),
            L("Heard something. Want it?", "...Got news."),
            L("...Thanks. Really.", "Thanks."),
            L("You okay?", "...You look tired."),
            L("...", "Leave me alone."),
            L("Right. You're busy.", "...Whatever.")),
        ["Shane"] = new(
            L("...Hey.", "Oh. It's you.", "Hey."),
            L("Haven't seen you around.", "...You're back."),
            L("Heard something. Whatever.", "Got news, if you care."),
            L("...Thanks.", "Thanks. I mean it."),
            L("You look rough.", "...Get some rest."),
            L("...Leave me alone.", "Not now."),
            L("Go hang out with them, then.", "...Figures.")),
        ["Vincent"] = new(
            L("Hi, {player}!", "Hello!", "Hey hey!"),
            L("Where were you?!", "You came back!"),
            L("Guess what I found!", "I have news!"),
            L("Thank you!", "Wow, thanks!"),
            L("Are you sick?", "Are you okay?"),
            L("Hmph!", "I'm mad at you!"),
            L("You don't play with me anymore!", "Hmph.")),
        ["Willy"] = new(
            L("Ahoy, {player}!", "Mornin', friend.", "Ahoy there!"),
            L("Haven't seen you at the docks!", "There ye are!"),
            L("Heard somethin' on the tide.", "Got news, friend."),
            L("Thank ye kindly.", "Much obliged, {player}."),
            L("Ye look weathered.", "Take it easy, friend."),
            L("Hmph. Not now.", "...Ahoy."),
            L("Found new friends ashore, eh?", "Hmph.")),
        ["Wizard"] = new(
            L("Ah, {player}.", "Greetings.", "The stars foretold you."),
            L("Your absence was noted.", "You return. As foreseen."),
            L("I have seen a portent.", "Listen. I have news."),
            L("Your gift is... appreciated.", "My thanks."),
            L("Your spirit is weary.", "Rest. The stars insist."),
            L("Begone.", "I have no time for you."),
            L("Your attentions wander.", "Hmph.")),
    };

    /// <summary>The villager's lines for a feeling, or an empty set (then the plain lines).</summary>
    public static string[] For(string npc, Motive motive, bool hostile)
        => All.TryGetValue(npc, out BubbleVoice? v) ? v.For(motive, hostile) : Array.Empty<string>();
}
