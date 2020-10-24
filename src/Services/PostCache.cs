namespace Miniblog.Core.Services
{
    using Microsoft.Extensions.Options;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Table;

    using Miniblog.Core.Options;

    using Models;

    using System;
    using System.Collections.Generic;

    public class PostCache : IPostCache
    {
        private readonly List<Post> _posts;
        private readonly AzureStorageOptions _options;

        public PostCache(IOptionsMonitor<AzureStorageOptions> optionsMonitor)
        {
            this._posts = new List<Post>();
            this._options = optionsMonitor?.CurrentValue ?? throw new ArgumentNullException(nameof(optionsMonitor));

            this.Initialize();
        }

        public IEnumerable<Post> GetPosts() => this._posts;

        public void RemovePost(Post post)
        {
            if (this._posts.Contains(post))
            {
                this._posts.Remove(post);
            }
        }

        public void AddPost(Post post)
        {
            RemovePost(post);

            this._posts.Add(post);
            this.SortCache();
        }

        public void SortCache() =>
            this._posts.Sort((p1, p2) => p2.PubDate.CompareTo(p1.PubDate));

        private void Initialize()
        {
            var storageAccount = CloudStorageAccount.Parse(_options.ConnectionString);
            var tableClient = storageAccount.CreateCloudTableClient();
            var table = tableClient.GetTableReference(_options.TableName);

            TableContinuationToken? token = null;

            do
            {
                var queryResult = table.ExecuteQuerySegmentedAsync(new TableQuery<Post>(), token)
                    .GetAwaiter()
                    .GetResult();

                this._posts.AddRange(queryResult.Results);
                token = queryResult.ContinuationToken;

            } while (token != null);
        }
    }
}
