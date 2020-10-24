namespace Miniblog.Core.Models
{
    using Microsoft.WindowsAzure.Storage.Table;

    public class PostEntity : TableEntity
    {
        public PostEntity() { }

        public PostEntity(string id, string slug)
        {
            this.PartitionKey = id;
            this.RowKey = slug;
        }
    }
}
