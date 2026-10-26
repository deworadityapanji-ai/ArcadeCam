using System;
using System.Collections.Generic;

namespace ArcadeCam.Helpers;

// A visibility snapshot lasts until the caller explicitly restores it. Repeated
// suppression must never replace an original 'on' state with our temporary 'off'.
internal sealed class VisibilityOverride<T> where T : class
{
    private readonly Dictionary<T, bool> _saved = new();
    private readonly Func<T, bool> _alive, _read;
    private readonly Action<T, bool> _write;

    internal VisibilityOverride(Func<T, bool> alive, Func<T, bool> read, Action<T, bool> write)
    { _alive = alive; _read = read; _write = write; }

    internal void Hide(T node)
    {
        if (node == null || !_alive(node)) return;
        if (!_saved.ContainsKey(node)) _saved.Add(node, _read(node));
        if (_read(node)) _write(node, false);
    }

    internal void Reapply()
    {
        foreach (T node in _saved.Keys)
            if (_alive(node) && _read(node)) _write(node, false);
    }

    internal void Restore()
    {
        foreach (var entry in _saved)
            if (_alive(entry.Key) && _read(entry.Key) != entry.Value) _write(entry.Key, entry.Value);
        _saved.Clear();
    }
}
