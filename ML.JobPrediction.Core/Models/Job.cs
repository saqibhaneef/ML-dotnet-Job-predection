using System.Runtime.Serialization;

namespace ML.JobPrediction.Core.Models
{
    [DataContract]
    public class Job
    {
        public Job() { }

        public Job(string description)
        {
            Description = description;
        }

        [DataMember(Name = "desc")]
        public string Description { get; set; }

        [DataMember(Name = "category")]
        public string Category { get; set; }
    }
}
