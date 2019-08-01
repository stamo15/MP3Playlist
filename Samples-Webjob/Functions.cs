using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.WindowsAzure.Storage.Table;
using MP3Playlist.Models;
using MP3Playlist.Services;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using NAudio.Wave;

namespace Samples_Webjob
{
    public class Functions
    {

        // accessor variables and methods for blob containers and queues
        private static BlobStorageService _blobStorageService = new BlobStorageService();
        private static CloudQueueService _queueStorageService = new CloudQueueService();

        private static CloudBlobContainer getPlaylistBlobContainer()
        {
            return _blobStorageService.getCloudBlobContainer();
        }

        private static CloudQueue getAudioMakerQueue()
        {
            return _queueStorageService.getCloudQueue();
        }

        // This function will get triggered/executed when a new message is written 
        // on an Azure Queue called queue.
        public static void GenerateSample(
        [QueueTrigger("samplequeue")] SampleEntity sampleInQueue,
        [Table("Samples", "{PartitionKey}", "{RowKey}")] SampleEntity sampleInTable,
        [Table("Samples")] CloudTable tableBinding, TextWriter logger)
        {
            logger.WriteLine("New message added to queue...");

            // Create a retrieve operation that takes a sample entity.
            TableOperation retrieveOperation = TableOperation.Retrieve<SampleEntity>(sampleInQueue.PartitionKey, sampleInQueue.RowKey);

            // Execute the retrieve operation.
            TableResult retrievedResult = tableBinding.Execute(retrieveOperation);
            if (retrievedResult.Result == null)
            {
                logger.WriteLine("Entity not found!");
            }
            else
            {
                sampleInTable = (SampleEntity)retrievedResult.Result;

                string name = string.Format("{0}{1}", Guid.NewGuid(), ".mp3");
                CloudBlockBlob inputBlob = getPlaylistBlobContainer().GetBlockBlobReference("uploads/" + sampleInTable.Mp3Blob);
                CloudBlockBlob outputBlob = getPlaylistBlobContainer().GetBlockBlobReference("samples/" + name);

                using (Stream input = inputBlob.OpenRead())
                using (Stream output = outputBlob.OpenWrite())
                {
                    CreateSample(input, output, 20);
                    outputBlob.Properties.ContentType = "audio/mpeg3";
                }
                sampleInTable.SampleMp3Blob = name;
                sampleInTable.SampleDate = DateTime.Now;

                // Create the TableOperation that inserts the sample entity.
                var updateOperation = TableOperation.InsertOrReplace(sampleInTable);

                // Execute the insert operation.
                tableBinding.Execute(updateOperation);
                logger.WriteLine("GenerateSample() completed...");
            }
            
        }

        private static void CreateSample(Stream input, Stream output, int duration)
        {
            using (var reader = new Mp3FileReader(input, wave => new NLayer.NAudioSupport.Mp3FrameDecompressor(wave)))
            {
                Mp3Frame frame;
                frame = reader.ReadNextFrame();
                int frameTimeLength = (int)(frame.SampleCount / (double)frame.SampleRate * 1000.0);
                int framesRequired = (int)(duration / (double)frameTimeLength * 1000.0);

                int frameNumber = 0;
                while ((frame = reader.ReadNextFrame()) != null)
                {
                    frameNumber++;

                    if (frameNumber <= framesRequired)
                    {
                        output.Write(frame.RawData, 0, frame.RawData.Length);
                    }
                    else break;
                }
            }
        }
    }
}
