namespace Miniblog.Core.Services
{
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Options;
    using Microsoft.WindowsAzure.Storage.Table;

    using Miniblog.Core.Models;
    using Miniblog.Core.Options;

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;

    public class AzureStorageBlogService : BlogServiceBase, IBlogService
    {
        private readonly AzureStorageOptions _options;
        private readonly CloudTableClient _cloudTableClient;

        public AzureStorageBlogService(
            IPostCache postCache,
            IWebHostEnvironment env,
            IHttpContextAccessor contextAccessor,
            IOptionsMonitor<AzureStorageOptions> optionsMonitor,
            CloudTableClient cloudTableClient)
            : base(postCache, env, contextAccessor)
        {
            this._options = optionsMonitor?.CurrentValue ?? throw new ArgumentNullException(nameof(optionsMonitor));
            this._cloudTableClient = cloudTableClient ?? throw new ArgumentNullException(nameof(cloudTableClient));
        }

        public void ForceRefresh()
        {
            var isAdmin = this.IsAdmin();

            if (isAdmin)
            {
                this.PostCache.Refresh();
            }
        }

        public IAsyncEnumerable<Post> GetPosts()
        {
            var isAdmin = this.IsAdmin();

            var posts = this.PostCache
                .GetPosts()
                .Where(p => p.PubDate <= DateTime.UtcNow && (p.IsPublished || isAdmin))
                .ToAsyncEnumerable();

            return posts;
        }

        public IAsyncEnumerable<Post> GetPosts(int count, int skip = 0)
        {
            var isAdmin = this.IsAdmin();

            var posts = this.PostCache
                .GetPosts()
                .Where(p => p.PubDate <= DateTime.UtcNow && (p.IsPublished || isAdmin))
                .Skip(skip)
                .Take(count)
                .ToAsyncEnumerable();

            return posts;
        }

        public Dictionary<int, List<Post>> GetPostsByYear()
        {
            var isAdmin = this.IsAdmin();

            return this.PostCache
                .GetPosts()
                .Where(p => p.PubDate <= DateTime.UtcNow && (p.IsPublished || isAdmin))
                .GroupBy(x => x.PubDate.Year)
                .ToDictionary(grp => grp.Key, grp => grp.ToList());
        }

        public Task<Post?> GetPostById(string id)
        {
            var isAdmin = this.IsAdmin();
            var post = this.PostCache
                .GetPosts()
                .FirstOrDefault(p => p.ID.Equals(id, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(
                post is null || post.PubDate > DateTime.UtcNow || (!post.IsPublished && !isAdmin)
                    ? null
                    : post);
        }

        public Task<Post?> GetPostBySlug(string slug)
        {
            var isAdmin = this.IsAdmin();
            var post = this.PostCache
                .GetPosts()
                .FirstOrDefault(p => p.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(
                post is null || post.PubDate > DateTime.UtcNow || (!post.IsPublished && !isAdmin)
                    ? null
                    : post);
        }

        public IAsyncEnumerable<Post> GetPostsByCategory(string category)
        {
            var isAdmin = this.IsAdmin();

            var posts = from p in this.PostCache.GetPosts()
                        where p.PubDate <= DateTime.UtcNow && (p.IsPublished || isAdmin)
                        where p.Categories.Contains(category, StringComparer.OrdinalIgnoreCase)
                        select p;

            return posts.ToAsyncEnumerable();
        }

        public IAsyncEnumerable<string> GetCategories()
        {
            var isAdmin = this.IsAdmin();

            return this.PostCache.GetPosts()
                .Where(p => p.IsPublished || isAdmin)
                .SelectMany(post => post.Categories)
                .Select(cat => cat.ToLowerInvariant())
                .Distinct()
                .ToAsyncEnumerable();
        }

        public async Task DeletePost(Post post)
        {
            if (post is null)
            {
                throw new ArgumentNullException(nameof(post));
            }

            var table = this._cloudTableClient.GetTableReference(this._options.TableName);
            var deleteOperation = TableOperation.Delete(post);
            await table.ExecuteAsync(deleteOperation).ConfigureAwait(false);

            this.PostCache.RemovePost(post);
        }

        public async Task<string> SaveFile(byte[] bytes, string fileName, string? suffix = null)
        {
            if (bytes is null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            suffix = CleanFromInvalidChars(suffix ??
                                           DateTime.UtcNow.Ticks.ToString(CultureInfo
                                               .InvariantCulture));
            // use blob storage here?
            var ext = Path.GetExtension(fileName);
            var name = CleanFromInvalidChars(Path.GetFileNameWithoutExtension(fileName));

            var fileNameWithSuffix = $"{name}_{suffix}{ext}";

            var absolute = Path.Combine(this.Folder, Files, fileNameWithSuffix);
            var dir = Path.GetDirectoryName(absolute);

            Directory.CreateDirectory(dir);
            await using var writer = new FileStream(absolute, FileMode.CreateNew);
            await writer.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);

            return $"/{Posts}/{Files}/{fileNameWithSuffix}";
        }

        public async Task SavePost(Post post)
        {
            if (post is null)
            {
                throw new ArgumentNullException(nameof(post));
            }

            post.LastModified = DateTime.UtcNow;
            post.SetupTableEntity();

            var table = this._cloudTableClient.GetTableReference(this._options.TableName);
            var insertOrMergeOperation = TableOperation.InsertOrMerge(post);
            var result = await table.ExecuteAsync(insertOrMergeOperation).ConfigureAwait(false);

            if (result.HttpStatusCode == (int)HttpStatusCode.NoContent)
            {
                this.PostCache.AddPost(post);
            }
        }

        protected override string GetFilePath(Post post) => Path.Combine(this.Folder, $"{post?.ID}.json");
    }
}
