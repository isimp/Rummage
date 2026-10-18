using System;
using System.Collections.Generic;
using System.IO;
using Rummage.Core;
using Xunit;
using static Rummage.Tests.Fixtures;

namespace Rummage.Tests;

/// <summary>
/// What the search box last found or took is kept in a small file per character, named by its
/// player id. A character with no file yet starts with nothing remembered. A file is written beside
/// the old one first and only then put in its place, so a write that fails leaves the old one whole.
/// A file that cannot be read or written is told in the log, once however often it happens, and the
/// search box goes on with what it holds.
/// </summary>
public sealed class RecentFileTests : IDisposable
{
    private const long You = 76561198000000001;
    private const long Friend = 76561198000000002;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "rummage-tests-" + Guid.NewGuid().ToString("N"), "last-used");
    private readonly List<string> _told = new List<string>();

    private RecentFile Files() => new RecentFile(_folder, _told.Add);

    private static Recent FoundWood()
    {
        var recent = new Recent();
        recent.Found(EntryKind.Item, Wood);
        return recent;
    }

    public void Dispose()
    {
        var root = Path.GetDirectoryName(_folder);
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void WhatIsWrittenIsReadBackForTheSameCharacter()
    {
        var files = Files();

        Assert.True(files.Write(You, FoundWood()));

        Assert.Equal(EnterDoes.TakeOne, Files().Read(You).Enter(EntryKind.Item, Wood, countTyped: false, shift: false));
        Assert.Empty(_told);
    }

    [Fact]
    public void EachCharacterHasItsOwnFile()
    {
        Files().Write(You, FoundWood());

        Assert.Empty(Files().Read(Friend).Entries);
        Assert.NotEqual(Files().PathOf(You), Files().PathOf(Friend));
    }

    [Fact]
    public void TheFileIsNamedByThePlayerIdInTheFolder()
    {
        Assert.Equal(Path.Combine(_folder, "76561198000000001.txt"), Files().PathOf(You));
    }

    [Fact]
    public void ACharacterWithNoFileYetStartsWithNothingRemembered()
    {
        Assert.Empty(Files().Read(You).Entries);
        Assert.Empty(_told);
    }

    [Fact]
    public void WritingMakesTheFolder()
    {
        Files().Write(You, FoundWood());

        Assert.True(File.Exists(Files().PathOf(You)));
        Assert.False(File.Exists(Files().PathOf(You) + ".tmp"));
    }

    /// <summary>
    /// The character's file made unreadable: on Windows held open with no sharing, as another
    /// program holding it would, elsewhere with its permissions taken away. Undone when disposed.
    /// </summary>
    private IDisposable Held(long player)
    {
        Files().Write(player, FoundWood());
        string path = Files().PathOf(player);
        if (OperatingSystem.IsWindows())
        {
            return new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }

        File.SetUnixFileMode(path, UnixFileMode.None);
        return new Undo(() => File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite));
    }

    private sealed class Undo : IDisposable
    {
        private readonly Action _undo;

        public Undo(Action undo)
        {
            _undo = undo;
        }

        public void Dispose() => _undo();
    }

    [Fact]
    public void AFileThatCannotBeReadIsToldAndNothingIsRemembered()
    {
        using var held = Held(You);

        Assert.Empty(Files().Read(You).Entries);
        Assert.Contains("Could not read", Assert.Single(_told), StringComparison.Ordinal);
    }

    [Fact]
    public void AWriteThatFailsIsToldAndLeavesTheOldFileWhole()
    {
        var files = Files();
        files.Write(You, FoundWood());
        var before = File.ReadAllText(files.PathOf(You));
        Directory.CreateDirectory(files.PathOf(You) + ".tmp");

        var changed = new Recent();
        changed.Took(EntryKind.Item, Stone);
        Assert.False(files.Write(You, changed));

        Assert.Equal(before, File.ReadAllText(files.PathOf(You)));
        Assert.Contains("Could not write", Assert.Single(_told), StringComparison.Ordinal);
    }

    [Fact]
    public void AFailureIsToldOnceHoweverOftenItHappens()
    {
        var files = Files();
        Directory.CreateDirectory(files.PathOf(You) + ".tmp");

        files.Write(You, FoundWood());
        files.Write(You, FoundWood());
        files.Read(Friend);

        Assert.Single(_told);
    }

    [Fact]
    public void ForgettingDeletesTheFile()
    {
        var files = Files();
        files.Write(You, FoundWood());

        files.Delete(You);

        Assert.False(File.Exists(files.PathOf(You)));
        Assert.Empty(files.Read(You).Entries);
    }

    [Fact]
    public void ForgettingACharacterWithNoFileIsNoFailure()
    {
        Files().Delete(You);

        Assert.Empty(_told);
    }

    [Fact]
    public void TheToldFailureNamesTheFolder()
    {
        using var held = Held(You);

        Files().Read(You);

        Assert.Contains(_folder, Assert.Single(_told), StringComparison.Ordinal);
    }
}
