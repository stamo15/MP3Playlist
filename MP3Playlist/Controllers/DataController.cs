using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using MP3Playlist.Models;
using MP3Playlist.Services;
using Swashbuckle.Swagger.Annotations;
using System;
using System.IO;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Web.Http;
using System.Web.Http.Description;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using System.Web;
using System.Net.Http;

namespace MP3Playlist.Controllers
{
    public class DataController : ApiController
    {
        //Accessor variables to setup and correctly access the table
        private const string partitionName = "Samples_Partition_1";

        private CloudStorageAccount storageAccount;
        private CloudTableClient tableClient;
        private CloudTable table;

        // accessor variables and methods for blob containers and queues
        private BlobStorageService _blobStorageService = new BlobStorageService();
        private CloudQueueService _queueStorageService = new CloudQueueService();

        public DataController()
        {
            storageAccount = CloudStorageAccount.Parse(ConfigurationManager.ConnectionStrings["AzureWebJobsStorage"].ToString());
            tableClient = storageAccount.CreateCloudTableClient();
            table = tableClient.GetTableReference("samples");
        }

        private CloudBlobContainer getPlaylistBlobContainer()
        {
            return _blobStorageService.getCloudBlobContainer();
        }

        private CloudQueue getAudioMakerQueue()
        {
            return _queueStorageService.getCloudQueue();
        }

        /// <summary>
        /// Get the sample blob
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        // GET: api/data/5
        [ResponseType(typeof(Sample))]
        public IHttpActionResult Get(string id)
        {
            // Create a retrieve operation that takes a sample entity.
            TableOperation retrieveOperation = TableOperation.Retrieve<SampleEntity>(partitionName, id);

            // Execute the retrieve operation.
            TableResult retrievedResult = table.Execute(retrieveOperation);
            if (retrievedResult.Result == null || ((SampleEntity)retrievedResult.Result).SampleMp3Blob == null)
            {
                // Return NOT FOUND if entity doesn't exist or does not have a sample blob
                return NotFound();
            } 
            else
            {
                // Retrieving the entity from the table
                SampleEntity sampleEntity = (SampleEntity)retrievedResult.Result;

                // Retrieving the blob from the "samples" directory
                var blob = getPlaylistBlobContainer().GetBlockBlobReference("samples/" + sampleEntity.SampleMp3Blob);
                Stream blobStream = blob.OpenRead();

                // Setting the response message containing the sample blob
                HttpResponseMessage message = new HttpResponseMessage(HttpStatusCode.OK);
                message.Content = new StreamContent(blobStream);
                message.Content.Headers.ContentLength = blob.Properties.Length;
                message.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg3");
                message.Content.Headers.ContentDisposition = new
                System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
                {
                    FileName = blob.Name,
                    Size = blob.Properties.Length
                };
                return ResponseMessage(message);
            }
        }

        /// <summary>
        /// Update the blob sample
        /// </summary>
        /// <param name="id"></param>
        /// <param name="sample"></param>
        /// /// <returns></returns>
        // PUT: api/data/5
        [SwaggerResponse(HttpStatusCode.NoContent)]
        [ResponseType(typeof(void))]
        public IHttpActionResult Put(string id)
        {
            // Create a retrieve operation that takes a sample entity.
            TableOperation retrieveOperation = TableOperation.Retrieve<SampleEntity>(partitionName, id);

            // Execute the retrieve operation.
            TableResult retrievedResult = table.Execute(retrieveOperation);
            if (retrievedResult.Result == null)
            {
                // Return NOT FOUND if entity doesn't exist
                return NotFound();
            }
            else
            {
                // Retrieving the entity from the table
                SampleEntity updateEntity = (SampleEntity)retrievedResult.Result;

                // Deleting old blobs for this entity before updating
                deleteOldBlobs(updateEntity);

                // Request containing original blob
                var request = HttpContext.Current.Request;

                // Initializing the newly uploaded blob and adding it to the "uploads" directory
                var name = string.Format("{0}{1}", Guid.NewGuid(), ".mp3");
                string path = "uploads/" + name;
                var blob = getPlaylistBlobContainer().GetBlockBlobReference(path);

                // Setting blob poreperties
                blob.Properties.ContentType = "audio/mpeg3";
                blob.UploadFromStream(request.InputStream);

                // Creating the sample url for this entity
                var baseUrl = Request.RequestUri.GetLeftPart(UriPartial.Authority);
                string sampleURL = baseUrl.ToString() + "/api/data/" + id;

                // Updating sample entity fields
                updateEntity.Mp3Blob = name;
                updateEntity.SampleMp3URL = sampleURL;
                updateEntity.SampleMp3Blob = null;
                updateEntity.SampleDate = (DateTime?) null;

                // Create the TableOperation that inserts the sample entity.
                var updateOperation = TableOperation.InsertOrReplace(updateEntity);

                // Execute the insert operation.
                table.Execute(updateOperation);

                //Adding message to queue
                var queueMessageSample = new SampleEntity(partitionName, id);
                getAudioMakerQueue().AddMessage(new CloudQueueMessage(JsonConvert.SerializeObject(queueMessageSample)));

                return StatusCode(HttpStatusCode.NoContent);
            }
        }

        private void deleteOldBlobs(SampleEntity sampleEntity)
        {
            // Create the TableOperation that updates the sample entity.
            var updateOperation = TableOperation.InsertOrReplace(sampleEntity);

            // Deleting uploaded MP3 blob
            if (sampleEntity.Mp3Blob != null)
            {
                var mp3Blob = getPlaylistBlobContainer().GetBlockBlobReference("uploads/" + sampleEntity.Mp3Blob);
                mp3Blob.DeleteIfExists();
                sampleEntity.Mp3Blob = null;
            }

            // Deleting sample MP3 blob
            if (sampleEntity.SampleMp3Blob != null)
            {
                var sampleBlob = getPlaylistBlobContainer().GetBlockBlobReference("samples/" + sampleEntity.SampleMp3Blob);
                sampleBlob.DeleteIfExists();
                sampleEntity.SampleMp3Blob = null;
                sampleEntity.SampleDate = null;
            }

            // Execute the update operation.
            table.Execute(updateOperation);
        }
    }
}
