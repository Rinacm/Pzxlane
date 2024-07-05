#region Copyright

// GPL v3 License
// 
// Pixeval/Pixeval
// Copyright (c) 2024 Pixeval/DownloadTaskGroup.cs
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.CoreApi.Model;
using Pixeval.Database;
using Pixeval.Database.Managers;
using Pixeval.Utilities;
using WinUI3Utilities;

namespace Pixeval.Download.Models;

public abstract partial class DownloadTaskGroup(DownloadHistoryEntry entry) : ObservableObject, IDownloadTaskGroup
{
    public DownloadHistoryEntry DatabaseEntry { get; } = entry;

    public abstract ValueTask InitializeTaskGroupAsync();

    public long Id => DatabaseEntry.Entry.Id;

    protected DownloadTaskGroup(IWorkEntry entry, string destination, DownloadItemType type) : this(new(destination, type, entry)) => SetNotCreateFromEntry();

    protected void SetNotCreateFromEntry()
    {
        if (!IsCreateFromEntry)
            return;
        IsCreateFromEntry = false;
        PropertyChanged += (sender, e) =>
        {
            var g = sender.To<DownloadTaskGroup>();
            if (e.PropertyName is not nameof(CurrentState))
                return;
            if (g.CurrentState is DownloadState.Running or DownloadState.Paused or DownloadState.Pending)
                return;
            g.DatabaseEntry.State = g.CurrentState;
            var manager = App.AppViewModel.AppServiceProvider.GetRequiredService<DownloadHistoryPersistentManager>();
            manager.Update(g.DatabaseEntry);
        };
        AfterItemDownloadAsync += async _ =>
        {
            if (IsAllCompleted && AfterAllDownloadAsync is not null)
            {
                IsPending = true;
                try
                {
                    await AfterAllDownloadAsync.Invoke(this, CancellationTokenSource.Token);
                }
                catch (TaskCanceledException)
                {
                    // ignored
                }
                IsPending = false;
            }
        };
        AfterAllDownloadAsync += AfterAllDownloadAsyncOverride;
    }

    private bool IsCreateFromEntry { get; set; } = true;

    protected abstract Task AfterAllDownloadAsyncOverride(DownloadTaskGroup sender, CancellationToken token = default);

    /// <inheritdoc cref="DownloadHistoryEntry.Destination"/>
    public string TokenizedDestination => DatabaseEntry.Destination;

    public int Count => TasksSet.Count;

    public IReadOnlyList<string> Destinations => TasksSet.Select(t => t.Destination).ToArray();

    protected IReadOnlyList<ImageDownloadTask> TasksSet => _tasksSet;

    private readonly List<ImageDownloadTask> _tasksSet = [];

    string IDownloadTaskBase.Destination => DatabaseEntry.Destination;

    public abstract string OpenLocalDestination { get; }

    public void TryReset()
    {
        if (CurrentState is not (DownloadState.Completed or DownloadState.Error or DownloadState.Cancelled))
            return;
        IsProcessing = true;
        TasksSet.ForEach(t => t.TryReset());
        if (CancellationTokenSource.IsCancellationRequested)
        {
            CancellationTokenSource.Dispose();
            CancellationTokenSource = new();
        }
        IsProcessing = false;
    }

    public void Pause()
    {
        IsProcessing = true;
        TasksSet.ForEach(t => t.Pause());
        IsProcessing = false;
    }

    public void TryResume()
    {
        IsProcessing = true;
        TasksSet.ForEach(t => t.TryResume());
        IsProcessing = false;
    }

    /// <summary>
    /// 因为<see cref="DownloadState.Pending"/>只能变为<see cref="DownloadState.Cancelled"/>而不能变为<see cref="DownloadState.Paused"/>，所以只需要在此使用
    /// </summary>
    public void Cancel()
    {
        if (CurrentState is not (DownloadState.Paused or DownloadState.Pending or DownloadState.Running or DownloadState.Queued))
            return;
        IsProcessing = true;
        TasksSet.ForEach(t => t.Cancel());
        CancellationTokenSource.Cancel();
        IsProcessing = false;
    }

    public abstract void Delete();

    /// <summary>
    /// 用于<see cref="DownloadState.Pending"/>的取消令牌
    /// </summary>
    private CancellationTokenSource CancellationTokenSource { get; set; } = new();

    /// <summary>
    /// 本下载组是否处于<see cref="DownloadState.Pending"/>状态
    /// </summary>
    [ObservableProperty][NotifyPropertyChangedFor(nameof(CurrentState))] private bool _isPending;

    [ObservableProperty] private bool _isProcessing;

    public DownloadState CurrentState
    {
        get
        {
            if (IsCreateFromEntry)
                return DatabaseEntry.State;
            var isError = false;
            var isRunning = false;
            var isPaused = false;
            var isCancelled = false;
            var isQueued = false;
            var isPending = false;
            foreach (var task in TasksSet)
                switch (task.CurrentState)
                {
                    case DownloadState.Queued:
                        isQueued = true;
                        break;
                    case DownloadState.Running:
                        isRunning = true;
                        break;
                    case DownloadState.Paused:
                        if (isCancelled)
                            ThrowHelper.ArgumentOutOfRange(CurrentState);
                        isPaused = true;
                        break;
                    case DownloadState.Cancelled:
                        if (isPaused)
                            ThrowHelper.ArgumentOutOfRange(CurrentState);
                        isCancelled = true;
                        break;
                    case DownloadState.Error:
                        isError = true;
                        break;
                    case DownloadState.Pending:
                        isPending = true;
                        break;
                    default:
                        break;
                }

            return isError ? DownloadState.Error :
                isCancelled ? DownloadState.Cancelled :
                isPaused ? DownloadState.Paused :
                isRunning ? DownloadState.Running :
                isQueued ? DownloadState.Queued :
                isPending || IsPending ? DownloadState.Pending :
                DownloadState.Completed;
        }
    }

    public event Func<ImageDownloadTask, Task>? ItemDownloadStartedAsync;

    public event Func<ImageDownloadTask, Task>? ItemDownloadStoppedAsync;

    public event Func<ImageDownloadTask, Task>? ItemDownloadErrorAsync;

    public event Func<ImageDownloadTask, Task>? AfterItemDownloadAsync;

    public event Func<DownloadTaskGroup, CancellationToken, Task>? AfterAllDownloadAsync;

    protected void AddToTasksSet(ImageDownloadTask task)
    {
        _tasksSet.Add(task);
        task.AfterDownloadAsync += x => AfterItemDownloadAsync?.Invoke(x) ?? Task.CompletedTask;
        task.DownloadStartedAsync += x => ItemDownloadStartedAsync?.Invoke(x) ?? Task.CompletedTask;
        task.DownloadStoppedAsync += x => ItemDownloadStoppedAsync?.Invoke(x) ?? Task.CompletedTask;
        task.DownloadErrorAsync += x => ItemDownloadErrorAsync?.Invoke(x) ?? Task.CompletedTask;
        task.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName switch
        {
            nameof(ImageDownloadTask.CurrentState) => nameof(CurrentState),
            nameof(ImageDownloadTask.ProgressPercentage) => nameof(ProgressPercentage),
            nameof(ImageDownloadTask.ErrorCause) => nameof(ErrorCause),
            _ => ""
        });
    }

    public Exception? ErrorCause => TasksSet.FirstOrDefault(t => t.ErrorCause is not null)?.ErrorCause;

    public bool IsAllCompleted => TasksSet.All(t => t.CurrentState is DownloadState.Completed);

    public bool IsAnyError => TasksSet.Any(t => t.CurrentState is DownloadState.Error);

    public double ProgressPercentage => TasksSet.Count is 0 ? 100 : TasksSet.Average(t => t.ProgressPercentage);

    public IEnumerator<ImageDownloadTask> GetEnumerator() => TasksSet.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        foreach (var task in TasksSet)
            task.Dispose();
    }
}
