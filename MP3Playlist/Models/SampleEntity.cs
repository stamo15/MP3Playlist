using Microsoft.WindowsAzure.Storage.Table;
using System;



namespace MP3Playlist.Models
{
    public class SampleEntity : TableEntity
    {
        // Declaring accessor fields for Sample Entity
        public string Title { get; set; }
        public string Artist { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string Mp3Blob { get; set; }
        public string SampleMp3Blob { get; set; }
        public string SampleMp3URL { get; set; }
        public DateTime? SampleDate { get; set; }

        // Declaring several Constructors

        public SampleEntity(string partitionKey, string sampleID)
        {
            PartitionKey = partitionKey;
            RowKey = sampleID;
        }

        public SampleEntity(string partitionKey, string sampleID, string Title, string Artist, DateTime? createdDate)
        {
            PartitionKey = partitionKey;
            RowKey = sampleID;
            this.Title = Title;
            this.Artist = Artist;
            this.CreatedDate = createdDate;
        }

        public SampleEntity() { }
    }
}