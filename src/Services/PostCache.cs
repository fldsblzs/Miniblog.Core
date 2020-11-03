namespace Miniblog.Core.Services
{
    using Microsoft.Extensions.Options;
    using Microsoft.WindowsAzure.Storage.Table;

    using Miniblog.Core.Options;

    using Models;

    using System;
    using System.Collections.Generic;

    public class PostCache : IPostCache
    {
        private readonly AzureStorageOptions _options;
        private readonly CloudTableClient _cloudTableClient;

        private DateTimeOffset _timeStamp;
        private List<Post> _posts;

        public PostCache(
            IOptionsMonitor<AzureStorageOptions> optionsMonitor,
            CloudTableClient cloudTableClient)
        {
            this._posts = new List<Post>();
            this._options = optionsMonitor?.CurrentValue ?? throw new ArgumentNullException(nameof(optionsMonitor));
            this._cloudTableClient = cloudTableClient ?? throw new ArgumentNullException(nameof(cloudTableClient));
            this._timeStamp = this.RefreshCache();
        }

        public IEnumerable<Post> GetPosts()
        {
            if (DateTimeOffset.UtcNow > this._timeStamp.AddHours(1))
            {
                this._timeStamp = this.RefreshCache();
            }

            return this._posts;
        }

        public void RemovePost(Post post)
        {
            if (this._posts.Contains(post))
            {
                this._posts.Remove(post);
            }
        }

        public void AddPost(Post post)
        {
            this.RemovePost(post);

            this._posts.Add(post);
            this.SortCache();
        }

        public void SortCache() =>
            this._posts.Sort((p1, p2) => p2.PubDate.CompareTo(p1.PubDate));

        public void Refresh() =>
            RefreshCache();

        private DateTimeOffset RefreshCache()
        {
            var table = this._cloudTableClient.GetTableReference(_options.TableName);

            TableContinuationToken? token = null;
            var posts = new List<Post>();

            do
            {
                var queryResult = table.ExecuteQuerySegmentedAsync(new TableQuery<Post>(), token)
                    .GetAwaiter()
                    .GetResult();

                posts.AddRange(queryResult.Results);
                token = queryResult.ContinuationToken;

            } while (token != null);

            this._posts = posts;

            return DateTimeOffset.UtcNow;
        }
    }
}
