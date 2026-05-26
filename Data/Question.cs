using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace fishingWithStudy.Data
{
    public class QuestionCategory
    {
        [JsonPropertyName("category")]
        public string Category { get; set; } = "";

        [JsonPropertyName("category_i18n")]
        public string CategoryI18n { get; set; } = "";

        [JsonPropertyName("questionList")]
        public List<Question> QuestionList { get; set; } = new();
    }

    public class Question
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("type")]
        public string Type { get; set; } = "single";

        [JsonPropertyName("question")]
        public string QuestionText { get; set; } = "";

        [JsonPropertyName("options")]
        public List<Option> Options { get; set; } = new();

        [JsonPropertyName("answer")]
        public List<string> Answer { get; set; } = new();
    }

    public class Option
    {
        [JsonPropertyName("tag")]
        public string Tag { get; set; } = "";

        [JsonPropertyName("text")]
        public string Text { get; set; } = "";
    }
}