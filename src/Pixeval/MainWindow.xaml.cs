﻿#region Copyright (c) Pixeval/Pixeval
// GPL v3 License
// 
// Pixeval/Pixeval
// Copyright (c) 2022 Pixeval/MainWindow.xaml.cs
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
#endregion

using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Pixeval.CoreApi;
using System;
using System.Threading.Tasks;

namespace Pixeval;

[LocalizedStringResources()]
internal sealed partial class MainWindow
{
    private readonly ISessionRefresher _sessionRefresher;
    public MainWindow(ISessionRefresher sessionRefresher)
    {
        _sessionRefresher = sessionRefresher;
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppTitleBarText.Text = SR.AppName;
    }

    private void MainWindow_OnClosed(object sender, WindowEventArgs args)
    {
        Environment.Exit(0);
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        await _sessionRefresher.GetAccessTokenAsync();
    }
}