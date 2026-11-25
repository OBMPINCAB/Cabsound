namespace CabSound.Core;

/// <summary>
/// Which output is the cabinet's sound card: the saved endpoint ID if it still exists, else a name
/// match (Windows renumbers USB cards, e.g. "2-" → "3-"), preferring the "Speakers" output over a
/// digital output with the same card name.
/// </summary>
public static class SoundCardChoice
{
    public static string? Pick(IReadOnlyList<(string Id, string Name)> devices, string? id, string nameHint)
    {
        if (id is not null && devices.Any(d => d.Id == id)) return id;

        var matches = devices.Where(d => d.Name.Contains(nameHint, StringComparison.OrdinalIgnoreCase)).ToList();
        var speakers = matches.Where(d => d.Name.StartsWith("Speakers", StringComparison.OrdinalIgnoreCase)).Select(d => d.Id);
        return speakers.FirstOrDefault() ?? matches.Select(d => d.Id).FirstOrDefault();
    }

    /// <summary>
    /// The name to remember for an output the owner picked: the card's name without the output
    /// ("Speakers (...)") and without Windows' port number ("3- "), so it still matches after a renumber.
    /// </summary>
    public static string NameHint(string friendlyName)
    {
        int open = friendlyName.IndexOf('('), close = friendlyName.LastIndexOf(')');
        var card = open >= 0 && close > open ? friendlyName[(open + 1)..close] : friendlyName;
        card = card.Trim();

        var dash = card.IndexOf('-');
        var numbered = dash > 0 && card[..dash].All(char.IsDigit);
        return numbered ? card[(dash + 1)..].Trim() : card;
    }
}
