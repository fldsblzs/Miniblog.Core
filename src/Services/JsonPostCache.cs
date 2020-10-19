namespace Miniblog.Core.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Hosting;
    using Models;

    public class JsonPostCache : IPostCache
    {
        private readonly string _folder;
        private readonly List<Post> _posts;

        public JsonPostCache(IWebHostEnvironment env)
        {
            if (env is null)
            {
               throw new ArgumentNullException(nameof(env));
            }

            this._folder = Path.Combine(env.WebRootPath, "Posts");
            this._posts = new List<Post>();

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
            if (!this._posts.Contains(post))
            {
                this._posts.Add(post);
                this.SortCache();
            }
        }

        public void SortCache() =>
            this._posts.Sort((p1, p2) => p2.PubDate.CompareTo(p1.PubDate));

        private void Initialize()
        {
            if (!Directory.Exists(this._folder))
            {
                Directory.CreateDirectory(this._folder);
            }

            var jsonFiles =
                Directory.EnumerateFiles(this._folder, "*.json", SearchOption.TopDirectoryOnly);

            var concurrentBag = new ConcurrentBag<Post>();

            Parallel.ForEach(jsonFiles, (jsonFile) =>
            {
                var jsonString = File.ReadAllText(jsonFile);
                var post = JsonSerializer.Deserialize<Post>(jsonString);

                using var jsonDocument = JsonDocument.Parse(jsonString);
                var root = jsonDocument.RootElement;
                var categoriesElement = root.GetProperty("Categories");

                foreach (var categoryElement in categoriesElement.EnumerateArray())
                {
                    post.Categories.Add(categoryElement.GetString());
                }

                var commentsElement = root.GetProperty("Comments");

                foreach (var commentElement in commentsElement.EnumerateArray())
                {
                    var comment = JsonSerializer.Deserialize<Comment>(commentElement.ToString());
                    post.Comments.Add(comment);
                }

                concurrentBag.Add(post);
            });

            this._posts.AddRange(concurrentBag);
        }
    }
}
