namespace Endjin.CucumberJs.TestAdapter
{
    using Newtonsoft.Json;

    public class CucumberJsResult
    {
        [JsonProperty("keyword")]
        public string Keyword { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("elements")]
        public Element[] Elements { get; set; }

        [JsonProperty("uri")]
        public string Uri { get; internal set; }

        public class Element
        {
            [JsonProperty("keyword")]
            public string Keyword { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("description")]
            public string Description { get; set; }

            [JsonProperty("steps")]
            public Step[] Steps { get; set; }

            public class Step
            {
                [JsonProperty("keyword")]
                public string Keyword { get; set; }

                [JsonProperty("line")]
                public int LineNumber { get; internal set; }

                [JsonProperty("name")]
                public string Name { get; set; }

                [JsonProperty("result")]
                public StepResult Result { get; set; }

                public class StepResult
                {
                    [JsonProperty("status")]
                    public string Status { get; set; }

                    [JsonProperty("error_message")]
                    public string ErrorMessage { get; set; }

                    [JsonProperty("duration")]
                    public long Duration { get; set; }
                }
            }
        }
    }
}