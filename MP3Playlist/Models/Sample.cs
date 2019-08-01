using System;
using System.ComponentModel.DataAnnotations;

namespace MP3Playlist.Models
{
    public class Sample
    {
        /// <summary>
        /// Sample ID
        /// </summary>
        public string SampleID { get; set; }

        /// <summary>
        /// Title of sample
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Name of Artist
        /// </summary>
        public string Artist { get; set; }

        /// <summary>
        /// Creation date/time of entity
        /// </summary>
        public DateTime? CreatedDate { get; set; }

        /// <summary>
        /// Name of uploaded blob in storage
        /// </summary>
        public string Mp3Blob { get; set; }

        /// <summary>
        /// Name of sample blob in storage
        /// </summary>
        public string SampleMp3Blob { get; set; }

        /// <summary>
        /// Web service resource URL of MP3 sample
        /// </summary>
        public string SampleMp3URL { get; set; }

        /// <summary>
        /// Creation date/time of sample blob
        /// </summary>
        public DateTime? SampleDate { get; set; }
    }
}