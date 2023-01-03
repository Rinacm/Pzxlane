﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pixeval.CoreApi;
using Pixeval.CoreApi.Models;
using Pixeval.CoreApi.Services;
using Pixeval.Storage;
using WinUIEx;

namespace Pixeval;

internal class SessionRefresher : ISessionRefresher
{
    private readonly SessionStorage _storage;
    private readonly IPixivAuthService _authService;

    public SessionRefresher(SessionStorage storage, IPixivAuthService authService)
    {
        _storage = storage;
        _authService = authService;
    }

    public async Task<string> GetAccessTokenAsync(string? refreshToken = null)
    {
        TokenResponse response;
        if (refreshToken is not null)
        {
            response = await _authService.RefreshAsync(refreshToken);
        }
        else
        {
            try
            {
                var session = await _storage.GetSessionAsync();
                if (session is not null)
                {
                    if (session.Updated - DateTimeOffset.Now < TimeSpan.FromSeconds(3600))
                    {
                        response = await _authService.RefreshAsync(session.RefreshToken);
                    }
                    else
                    {
                        throw new Exception();
                    }
                }
                else
                {
                    throw new Exception();
                }
            }
            catch
            {
                response = await OpenLoginWindowAsync();
            }

        }
        await _storage.SetSessionAsync(response.User.Id, response.RefreshToken, response.AccessToken).ConfigureAwait(false);
        return response.AccessToken;
    }

    private async Task<TokenResponse> OpenLoginWindowAsync()
    {
        var loginWindow = new LoginWindow();
        loginWindow.Activate();
        var response = await loginWindow.LoginTask.ContinueWith(task =>
        {
            if (task.IsCompletedSuccessfully)
            {
                loginWindow.Close();
            }
            return task.Result;
        },TaskScheduler.Default);
        return response;
    }
}