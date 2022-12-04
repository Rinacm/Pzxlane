﻿using Pixeval.CoreApi;
using Pixeval.Data;
using System;
using System.Threading.Tasks;

namespace Pixeval.Storage
{
    internal class SessionStorage
    {
        private string? _userId;
        private readonly IBaseRepository<UserSession> _sessionRepository;

        public SessionStorage(IBaseRepository<UserSession> sessionRepository)
        {
            _sessionRepository = sessionRepository;
        }

        public async Task<UserSession?> GetSessionAsync(string? userId = null)
        {
#nullable disable
            if (userId is not null)
            {
                return await _sessionRepository.Collection.FindOneAsync(_ => _.UserId == userId);
            }
            return await _sessionRepository.Collection.Query().OrderByDescending(_ => _.Updated).FirstOrDefaultAsync();
#nullable restore
        }

        public async Task SetSessionAsync(string userId, string refreshToken, string accessToken)
        {
            UserSession session;
            if (_userId is not null)
            {
                session = await _sessionRepository.Collection.FindOneAsync(_ => _.UserId == _userId);
                if (session is not null)
                {
                    session = session with { Updated = DateTimeOffset.Now, RefreshToken = refreshToken };
                    await _sessionRepository.UpdateAsync(session);
                }
            }
            session = new UserSession(userId, refreshToken, accessToken, DateTimeOffset.Now);
            await _sessionRepository.CreateAsync(session);
        }

        public Task ClearSessionAsync(string userId)
        {
            return _sessionRepository.Collection.DeleteManyAsync(_ => _.UserId == userId);
        }
    }
}
