using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Configuration;
using   MP3Playlist.Models;

namespace MP3Playlist.Migrations
{
    public static class InitialSamples
    {
        public static void createSamples()
        {
            const string partitionName = "Samples_Partition_1";

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.ConnectionStrings["AzureWebJobsStorage"].ToString());

            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();

            CloudTable table = tableClient.GetTableReference("samples");

            //Create table if it doesn't exist and populate it
            if (!table.Exists())
            {
                //Creating the table
                table.CreateIfNotExists();

                //Batch operation that inserts all samples in table
                TableBatchOperation batchOperation = new TableBatchOperation();

                string[] sampleTitles, sampleArtists;
                sampleTitles = new string[7] { "Aqualung", "Songs from the Wood", "Wish You Were Here", "Still Life", "Musical Box", "Supper's Ready", "Starship Trooper"};
                sampleArtists = new string[7] { "Jethro Tull", "Jethro Tull", "Pink Floyd", "Van der Graaf Generator", "Genesis", "Genesis", "Yes"};

                //Add seven sample entities to batch insert operation
                for (int index = 1; index < 8; index = index + 1)
                {
                    SampleEntity sampleEntity = new SampleEntity(partitionName, index.ToString());
                    sampleEntity.Title = sampleTitles[index - 1];
                    sampleEntity.Artist = sampleArtists[index - 1];
                    sampleEntity.CreatedDate = DateTime.Now;
                    sampleEntity.Mp3Blob = null;
                    sampleEntity.SampleMp3Blob = null;
                    sampleEntity.SampleMp3URL = null;
                    sampleEntity.SampleDate = (DateTime?)null;

                    batchOperation.Insert(sampleEntity);
                }
                //SampleEntity sampleEntity = new SampleEntity(partitionName, "1");
                //sampleEntity.Title = "Aqualung";
                //sampleEntity.Artist = "Jethro Tull";
                //sampleEntity.CreatedDate = DateTime.Now;
                //batchOperation.Insert(sampleEntity);

                //SampleEntity sampleEntity2 = new SampleEntity(partitionName, "2");
                //sampleEntity2.Title = "Songs from the Wood";
                //sampleEntity2.Artist = "Jethro Tull";
                //sampleEntity2.CreatedDate = DateTime.Now;
                //batchOperation.Insert(sampleEntity2);
                //Executing batch operation
                table.ExecuteBatch(batchOperation);
            }
        }
    }
}