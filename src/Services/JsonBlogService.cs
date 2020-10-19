namespace Miniblog.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;

    using Models;

    public class JsonBlogService : BlogServiceBase, IBlogService
    {
        public JsonBlogService(
            IPostCache postCache,
            IWebHostEnvironment env,
            IHttpContextAccessor contextAccessor)
            : base(postCache, env, contextAccessor)
        {
        }

        public Task DeletePost(Post post)
        {
            if (post is null)
            {
                throw new ArgumentNullException(nameof(post));
            }

            var filePath = this.GetFilePath(post);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            this.PostCache.RemovePost(post);

            return Task.CompletedTask;
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

        public IAsyncEnumerable<Post> GetPostsByCategory(string category)
        {
            var isAdmin = this.IsAdmin();

            var posts = from p in this.PostCache.GetPosts()
                        where p.PubDate <= DateTime.UtcNow && (p.IsPublished || isAdmin)
                        where p.Categories.Contains(category, StringComparer.OrdinalIgnoreCase)
                        select p;

            return posts.ToAsyncEnumerable();
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

            var jsonFilePath = Path.Combine(this.Folder, $"{post.ID}.json");

            await using var fileStream = File.Create(jsonFilePath);
            await JsonSerializer.SerializeAsync(fileStream, post)
                .ConfigureAwait(false);

            this.PostCache.AddPost(post);
        }

        protected override string GetFilePath(Post post) => Path.Combine(this.Folder, $"{post?.ID}.json");
    }
}
