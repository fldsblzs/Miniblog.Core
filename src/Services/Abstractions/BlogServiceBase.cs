namespace Miniblog.Core.Services
{
    using System;
    using System.IO;
    using System.Text.RegularExpressions;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Models;

    public abstract class BlogServiceBase
    {
        protected const string Files = "files";
        protected const string Posts = "Posts";

        private readonly IHttpContextAccessor _contextAccessor;

        protected BlogServiceBase(IPostCache postCache, IWebHostEnvironment env, IHttpContextAccessor contextAccessor)
        {
            if (env is null)
            {
                throw new ArgumentNullException(nameof(env));
            }

            this.Folder = Path.Combine(env.WebRootPath, Posts);
            this.PostCache = postCache ?? throw new ArgumentNullException(nameof(postCache));
            this._contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
        }

        protected IPostCache PostCache { get; }
        protected string Folder { get; }

        protected bool IsAdmin() => this._contextAccessor.HttpContext?.User?.Identity.IsAuthenticated == true;

        protected static string CleanFromInvalidChars(string input)
        {
            // ToDo: what we are doing here if we switch the blog from windows to unix system or
            // vice versa? we should remove all invalid chars for both systems

            var regexSearch = Regex.Escape(new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars()));
            var r = new Regex($"[{regexSearch}]");
            return r.Replace(input, string.Empty);
        }

        protected abstract string GetFilePath(Post post);
    }
}
