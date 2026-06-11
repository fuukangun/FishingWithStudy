using fishingWithStudy.Logic;

Translation.ResetForTests();

AssertEqual("missing.key", Translation.Get("missing.key"));
AssertEqual("Answer: Catfish", Translation.Get("Answer: {0}", "Catfish"));

if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "i18n", "default.json")))
    throw new InvalidOperationException("Missing i18n/default.json.");

if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "i18n", "zh.json")))
    throw new InvalidOperationException("Missing i18n/zh.json.");

Console.WriteLine("PASS");

static void AssertEqual(string expected, string actual)
{
    if (expected != actual)
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
}
