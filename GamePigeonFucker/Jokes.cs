public static class Jokes
{
    private static readonly string[] BuiltInJokes =
    [
        "What do you call cheese that isn't yours? Nacho cheese.",
        "Why did the scarecrow get promoted? He was outstanding in his field, again.",
        "What do you call a fish wearing a bowtie? Sofishticated.",
        "Why did the banana go to the doctor? It wasn't peeling well.",
        "What do you call a cow with no legs? Ground beef.",
        "Why did the toilet paper roll down the hill? To get to the bottom.",
    ];

    private static readonly string[] FixedJokes =
    [
        "Why was 6 afraid of 7? \nSix Seven!!",
        "What did the 6 say to the 7? \nSix Seven!!!",
        "Why did the 6 cross the road? \nTo get to 7",
        "What comes after 6? \n7. 💀",
        "What comes before 7? \n6. 💀",
        "Six?\nSeven 💀",
    ];

    private const string JokesFilePath = "jokes.txt";

    private static readonly List<string> AllJokes = LoadJokes();

    private static readonly List<string> UnseenJokes = [];

    private static float _fixedJokeThreshold = 0f;

    private static List<string> LoadJokes()
    {
        if (!File.Exists(JokesFilePath))
        {
            File.WriteAllLines(JokesFilePath, DefinedJokes);
        }

        List<string> jokes = [.. BuiltInJokes];
        jokes.AddRange(File.ReadAllLines(JokesFilePath).Where(line => !string.IsNullOrWhiteSpace(line)));

        return jokes;
    }


    public enum JokeOperation
    {
        send,
        add,
    }


    [Command("/joke", "Sends a random joke to the given recipient, or adds a new one to the pool, because you clearly need one")]
    public static async void Joke(
        [CommandParam(optional: true)] JokeOperation type,
        [CommandParam(optional: true), IntCondition(nameof(type), (long)JokeOperation.send)] string recipient,
        [CommandParam, IntCondition(nameof(type), (long)JokeOperation.add)] string text
    )
    {
        if (type == JokeOperation.add)
        {
            File.AppendAllLines(JokesFilePath, [text]);
            AllJokes.Add(text);
            UnseenJokes.Add(text);
            Console.WriteLine($"Added joke. Pool now has {AllJokes.Count} jokes.");
            return;
        }

        recipient ??= GamePigeonFucker.IncomingChatIdentifier;

        if (recipient == null)
        {
            Console.WriteLine("No recipient given and no incoming chat to reply to");
            return;
        }

        if (Account.ActiveAccount == null)
        {
            Console.WriteLine("No active account set");
            return;
        }

        string joke;
        if (Random.Shared.NextSingle() < _fixedJokeThreshold)
        {
            joke = FixedJokes[Random.Shared.Next(FixedJokes.Length)];
        }
        else
        {
            if (UnseenJokes.Count == 0)
                UnseenJokes.AddRange(AllJokes);

            int index = Random.Shared.Next(UnseenJokes.Count);
            joke = UnseenJokes[index];
            UnseenJokes.RemoveAt(index);
        }

        _fixedJokeThreshold = Math.Min(1f, _fixedJokeThreshold + 0.01f);

        Console.WriteLine(joke);
    }


    public static readonly string[] DefinedJokes =
    {
        "Why don't scientists trust atoms? Because they make up everything.",
        "Why don't eggs tell jokes? They'd crack each other up.",
        "I only know 25 letters of the alphabet. I don't know y.",
        "What do you call fake spaghetti? An impasta.",
        "Why did the bicycle fall over? It was two tired.",
        "What do you call a bear with no teeth? A gummy bear.",
        "Why can't you give Elsa a balloon? She'll let it go.",
        "I'm on a seafood diet. I see food and I eat it.",
        "Why did the golfer bring two pairs of pants? In case he got a hole in one.",
        "What do you call a fish with no eyes? A fsh.",
        "How does a penguin build its house? Igloos it together.",
        "I told my wife she was drawing her eyebrows too high. She looked surprised.",
        "Why did the coffee file a police report? It got mugged.",
        "What do you call a can opener that doesn't work? A can't opener.",
        "Why did the math book look sad? It had too many problems.",
        "What did one wall say to the other wall? I'll meet you at the corner.",
        "Why don't skeletons fight each other? They don't have the guts.",
        "I would avoid the sushi if I were you. It's a little fishy.",
        "What do you call a factory that makes okay products? A satisfactory.",
        "How do you organize a space party? You planet.",
        "What's orange and sounds like a parrot? A carrot.",
        "Why did the cookie go to the doctor? Because it was feeling crummy.",
        "What do you call a dinosaur that crashes his car? Tyrannosaurus wrecks.",
        "What did the ocean say to the beach? Nothing, it just waved.",
        "Why did the picture go to jail? It was framed.",
        "Why did the invisible man turn down the job offer? He couldn't see himself doing it.",
        "What do you call a sleeping dinosaur? A dino-snore.",
        "Why did the chicken join a band? Because it had the drumsticks.",
        "What's a pirate's favorite letter? You'd think it's R, but it's the C.",
        "Why did the belt get arrested? For holding up a pair of pants.",
        "What do you call a bear caught in the rain? A drizzly bear.",
        "Why did the students eat their homework? The teacher said it was a piece of cake.",
        "What did the janitor say when he jumped out of the closet? Supplies!",
        "Why did the stadium get hot after the game? All the fans left.",
        "What do you call a group of disorganized cats? A cat-astrophe.",
        "Why don't oysters share their pearls? Because they're shellfish.",
        "I ate a clock yesterday. It was very time-consuming.",
        "My refrigerator makes a weird noise. I think it’s thinking.",
        "I bought shoes from a drug dealer. I don’t know what he laced them with, but I’ve been tripping all day.",
        "I’m afraid of elevators. I’m taking steps to avoid them.",
        "My socks are always disappearing. I suspect the washing machine has a foot fetish.",
        "I don’t trust stairs. They’re always up to something.",
        "I tried to catch some fog. Mist.",
        "I have a joke about paper. It’s tearable.",
        "I tried folding a fitted sheet. It won.",
        "I have a joke about construction. I’m still working on it.",
        "I named my vacuum Voldemort. Nobody talks about it.",
        "I have a joke about pizza, but it’s too cheesy.",
        "My wallet is like an onion. Opening it makes me cry.",
        "I tried meditation. I spent ten minutes thinking about mozzarella sticks.",
        "My plants are doing great. They haven’t complained once.",
        "I bought a clock yesterday. It was about time.",
        "I’m excellent at multitasking. I can waste time in several ways simultaneously.",
        "I went to the doctor because I felt invisible. He said he couldn’t see me.",
        "My pillow is cold on both sides. Finally, a professional.",
        "I bought a banana. It came with its own packaging.",
        "I tried teaching my goldfish karate. It forgot everything.",
        "I told my freezer to chill out. It did.",
        "I have a joke about glue. I’m having trouble letting it go.",
        "My plants are doing great. They haven’t complained once.",
        "I bought a clock yesterday. It was about time.",
        "I’m excellent at multitasking. I can waste time in several ways simultaneously.",
        "I went to the doctor because I felt invisible. He said he couldn’t see me.",
        "My pillow is cold on both sides. Finally, a professional.",
        "I opened the fridge three times. Nothing changed. I respect the consistency.",
        "I tried teaching my goldfish karate. It forgot everything.",
        "I told my freezer to chill out. It did.",
        "My left shoe and right shoe don’t get along. They’re always putting their feet down.",
        "I bought a thesaurus, but I can’t find it. I have no words.",
        "My plants are doing great. They haven’t complained once.",
        "I bought a clock yesterday. It was about time.",
        "I’m excellent at multitasking. I can waste time in several ways simultaneously.",
        "I went to the doctor because I felt invisible. He said he couldn’t see me.",
        "I bought a new broom. It’s sweeping the competition.",
        "My alarm and I have different ideas about when the day starts.",
        "I put my laundry in the dryer. It came out warm and slightly more wrinkled. Technology.",
        "My refrigerator contains three sauces and half a lemon. I’m one ingredient away from a problem.",
        "I bought a smart lightbulb. It’s smarter than me, which isn’t difficult.",
        "I put my glasses somewhere safe. I’ll never see them again.",
        "I bought a bag of chips. It was mostly air. Very breathable snack.",
        "I bought a ruler. It’s been measuring up pretty well.",
        "What do you call cheese that isn't yours? Nacho cheese.",
        "Why did the scarecrow get promoted? He was outstanding in his field, again.",
        "What do you call a fish wearing a bowtie? Sofishticated.",
        "Why did the banana go to the doctor? It wasn't peeling well.",
        "What do you call a cow with no legs? Ground beef.",
        "Why did the toilet paper roll down the hill? To get to the bottom.",
    };
}
