using Cassandra.Data.Linq;
using Cassandra.Mapping;
using Cassandra;

public class WordService
{
    private AIValidationService aiValidationService;
    private Table<Word> wordTable;

    public WordService(AIValidationService aiValidationService, Cassandra.ISession session)
    {
        this.aiValidationService = aiValidationService;
        var mapping = new MappingConfiguration()
            .Define(new Map<Word>()
            .PartitionKey(t => t.Locale)
            .ClusteringKey(t => t.Phrase)
        );
        wordTable = new Table<Word>(session, mapping, "words_tested");
        wordTable.CreateIfNotExists();
    }

    public async Task<bool> IsCollectableWord(string locale, string phrase)
    {
        return (await IsCollectableWordExplanation(locale, phrase)).Item1;
    }
    public async Task<(bool, Word)> IsCollectableWordExplanation(string locale, string phrase)
    {
        var existing = wordTable.Where(o => o.Locale == locale && o.Phrase == phrase.ToLower()).FirstOrDefault().Execute();
        if (existing != null)
        {
            return (Allowed(existing), existing);
        }
        var word = await aiValidationService.IsCollectableWord(locale, phrase);
        if (word == null)
        {
            return (false, new());
        }
        word.Phrase = phrase.ToLower();
        wordTable.Insert(word).Execute();
        return (Allowed(word), word);

        static bool Allowed(Word existing)
        {
            return existing.CanMakePicture && existing.IsRealItem && !existing.IsAbbreviation && (!existing.IsPersonCityOrCompany || existing.IsProduct);
        }
    }

    public class Word
    {
        public string Locale { get; set; }
        public bool IsRealItem { get; set; }
        public bool CanMakePicture { get; set; }
        public bool IsAbbreviation { get; set; }
        public bool IsPersonCityOrCompany { get; set; }
        public bool IsProduct { get; set; }
        public string LocaleGuess { get; set; }
        public string Phrase { get; set; }
        public string Category { get; set; }
    }
}