using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using MP3Playlist.Models;
using MP3Playlist.Services;
using Swashbuckle.Swagger.Annotations;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Web.Http;
using System.Web.Http.Description;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;

namespace MP3Playlist.Controllers
{
    public class SamplesController : ApiController
    {
        //Accessor variables to setup and correctly access the table
        private const string partitionName = "Samples_Partition_1";

        private CloudStorageAccount storageAccount;
        private CloudTableClient tableClient;
        private CloudTable table;

        // accessor variables and methods for blob containers and queues
        private BlobStorageService _blobStorageService = new BlobStorageService();
        private CloudQueueService _queueStorageService = new CloudQueueService();

        public SamplesController()
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
        /// Get all samples
        /// </summary>
        /// <returns></returns>
        // GET: api/Samples
        public IEnumerable<Sample> Get()
        {
            TableQuery<SampleEntity> query = new TableQuery<SampleEntity>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionName));
            List<SampleEntity> entities = new List<SampleEntity>(table.ExecuteQuery(query));

            //Creating a list of samples to return
            IEnumerable<Sample> allSamplesList = from e in entities
                                                 select new Sample()
                                               {
                                                   SampleID = e.RowKey,
                                                   Title = e.Title,
                                                   Artist = e.Artist,
                                                   CreatedDate = e.CreatedDate,
                                                   Mp3Blob = e.Mp3Blob,
                                                   SampleMp3Blob = e.SampleMp3Blob,
                                                   SampleMp3URL = e.SampleMp3URL,
                                                   SampleDate = e.SampleDate
                                                 };
            return allSamplesList;
        }

        /// <summary>
        /// Get a single sample
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        // GET: api/Samples/5
        [ResponseType(typeof(Sample))]
        public IHttpActionResult Get(string id)
        {
            //Retrieval query
            TableOperation query = TableOperation.Retrieve<SampleEntity>(partitionName, id);

            //Executing retrieval operation
            TableResult operation = table.Execute(query);

            if (operation.Result == null)
            {
                // Return NOT FOUND if entity doesn't exist
                return NotFound();
            }
            else
            {
                // Retrieving the entity from the table
                SampleEntity sampleEntity = (SampleEntity)operation.Result;

                // Initialing DTO Sample object to be sent as a response
                Sample sample = new Sample()
                {
                    SampleID = sampleEntity.RowKey,
                    Title = sampleEntity.Title,
                    Artist = sampleEntity.Artist,
                    CreatedDate = sampleEntity.CreatedDate,
                    Mp3Blob = sampleEntity.Mp3Blob,
                    SampleMp3Blob = sampleEntity.SampleMp3Blob,
                    SampleMp3URL = sampleEntity.SampleMp3URL,
                    SampleDate = sampleEntity.SampleDate
                };
                return Ok(sample);
            }
        }

        /// <summary>
        /// Create a new sample
        /// </summary>
        /// <param name="sample"></param>
        // POST: api/Samples
        [SwaggerResponse(HttpStatusCode.Created)]
        [ResponseType(typeof(Sample))]
        public IHttpActionResult Post(Sample sample)
        {
            // Initialing DTO Sample object to be added to table
            SampleEntity sampleEntity = new SampleEntity()
            {
                RowKey = getNewMaxRowKeyValue(),
                PartitionKey = partitionName,
                Title = sample.Title,
                Artist = sample.Artist,
                CreatedDate = DateTime.Now,
                Mp3Blob = null,
                SampleMp3Blob = null,
                SampleMp3URL = null,
                SampleDate = (DateTime?) null
            };

            // Create the TableOperation that inserts the sample entity.
            var insertOperation = TableOperation.Insert(sampleEntity);

            // Execute the insert operation.
            table.Execute(insertOperation);

            return CreatedAtRoute("DefaultApi", new { id = sampleEntity.RowKey }, sampleEntity);
        }

        /// <summary>
        /// Update a sample
        /// </summary>
        /// <param name="id"></param>
        /// <param name="sample"></param>
        /// /// <returns></returns>
        // PUT: api/Samples/5
        [SwaggerResponse(HttpStatusCode.NoContent)]
        [ResponseType(typeof(void))]
        public IHttpActionResult Put(string id, Sample sample)
        {
            if (id != sample.SampleID)
            {
                // Return BAD REQUEST if entity doesn't exist
                return BadRequest();
            }

            // Create a retrieve operation that takes a sample entity.
            TableOperation retrieveOperation = TableOperation.Retrieve<SampleEntity>(partitionName, id);

            // Execute the operation.
            TableResult retrievedResult = table.Execute(retrieveOperation);

            // Assign the result to a SampleEntity object.
            SampleEntity updateEntity = (SampleEntity)retrievedResult.Result;
            
            // Getting rid of any old blobs
            deleteOldBlobs(updateEntity);

            //Updating fields
            updateEntity.Title = sample.Title;
            updateEntity.Artist = sample.Artist;
            updateEntity.CreatedDate = sample.CreatedDate;
            updateEntity.Mp3Blob = null;
            updateEntity.SampleMp3Blob = null;
            updateEntity.SampleMp3URL = null;
            updateEntity.SampleDate = (DateTime?)null;

            // Create the TableOperation that inserts the sample entity.
            var updateOperation = TableOperation.InsertOrReplace(updateEntity);

            // Execute the insert operation.
            table.Execute(updateOperation);

            return StatusCode(HttpStatusCode.NoContent);
        }

        /// <summary>
        /// Delete a sample
        /// </summary>
        /// <param name="id"></param>
        /// /// <returns></returns>
        // DELETE: api/Samples/5
        [ResponseType(typeof(Sample))]
        public IHttpActionResult Delete(string id)
        {

            // Create a retrieve operation that takes a sample entity.
            TableOperation retrieveOperation = TableOperation.Retrieve<SampleEntity>(partitionName, id);

            // Execute the retrieve operation.
            TableResult retrievedResult = table.Execute(retrieveOperation);
            if (retrievedResult.Result == null) return NotFound();
            else
            {
                SampleEntity deleteEntity = (SampleEntity)retrievedResult.Result;
                
                // Getting rid of any old blobs
                deleteOldBlobs(deleteEntity);

                //Initializing operation
                TableOperation deleteOperation = TableOperation.Delete(deleteEntity);

                // Execute the operation.
                table.Execute(deleteOperation);

                return Ok(retrievedResult.Result);
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

        private String getNewMaxRowKeyValue()
        {
            TableQuery<SampleEntity> query = new TableQuery<SampleEntity>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionName));

            int maxRowKeyValue = 0;
            // Going through all samples to identify entity with max ID
            foreach (SampleEntity entity in table.ExecuteQuery(query))
            {
                int entityRowKeyValue = Int32.Parse(entity.RowKey);
                if (entityRowKeyValue > maxRowKeyValue) maxRowKeyValue = entityRowKeyValue;
            }
            maxRowKeyValue++;
            return maxRowKeyValue.ToString();
        }
    }
}
