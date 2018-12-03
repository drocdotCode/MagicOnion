﻿using MessagePack;
using System;
using System.Threading.Tasks;

namespace MagicOnion.Server.Hubs
{
    public interface IGroupRepositoryFactory
    {
        IGroupRepository CreateRepository(IFormatterResolver resolver);
    }

    public interface IGroupRepository
    {
        IGroup GetOrAdd(string groupName);
        bool TryGet(string groupName, out IGroup group);
        bool TryRemove(string groupName);
    }

    public interface IGroup
    {
        string GroupName { get; }
        ValueTask AddAsync(ServiceContext context);
        // Return Bool is removed from parent.
        ValueTask<bool> RemoveAsync(ServiceContext context);
        Task WriteAllAsync<T>(int methodId, T value);
        Task WriteExceptAsync<T>(int methodId, T value, Guid connectionId);
        Task WriteExceptAsync<T>(int methodId, T value, Guid[] connectionIds);
        Task WriteRawAsync(ArraySegment<byte> message, Guid[] exceptConnectionIds);
    }

    public static class GroupRepositoryExtensions
    {
        public static async ValueTask<IGroup> AddAsync(this IGroupRepository repository, string groupName, ServiceContext context)
        {
            var group = repository.GetOrAdd(groupName);
            await group.AddAsync(context).ConfigureAwait(false);
            return group;
        }
    }
}