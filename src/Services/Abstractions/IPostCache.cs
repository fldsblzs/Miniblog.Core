namespace Miniblog.Core.Services
{
    using System.Collections.Generic;
    using Models;

    public interface IPostCache
    {
        IEnumerable<Post> GetPosts();

        void RemovePost(Post post);

        void AddPost(Post post);

        void SortCache();

        void Refresh();
    }
}
