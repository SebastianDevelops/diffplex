﻿using DiffPlex.DiffBuilder.Model;
using DiffPlex.Model;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace DiffPlex.UI;

internal class InternalUtilities
{
    public const string FontFamily = "Cascadia Code, Consolas, Courier New, monospace, Microsoft Yahei, Microsoft Jhenghei, Meiryo, Segoe UI, Segoe UI Emoji, Segoe UI Symbol";

    public static readonly SolidColorBrush InsertBackground = new(Color.FromArgb(64, 96, 216, 32));

    public static readonly SolidColorBrush DeleteBackground = new(Color.FromArgb(64, 216, 32, 32));

    public static readonly SolidColorBrush GrayBackground = new(Color.FromArgb(32, 128, 128, 128));

    public static List<TextHighlighter> GetTextHighlighter(List<DiffPiece> sub, ChangeType modify, Brush foreground)
    {
        if (sub == null) return null;
        var insert = new TextHighlighter
        {
            Foreground = foreground,
            Background = InsertBackground
        };
        var delete = new TextHighlighter
        {
            Foreground = foreground,
            Background = DeleteBackground
        };
        var i = 0;
        foreach (var piece in sub)
        {
            var s = piece.Text;
            if (string.IsNullOrEmpty(s)) continue;
            var pt = piece.Type;
            if (pt == ChangeType.Modified) pt = modify;
            switch (piece.Type)
            {
                case ChangeType.Inserted:
                    Add(insert, i, piece.Text.Length);
                    break;
                case ChangeType.Deleted:
                    Add(delete, i, piece.Text.Length);
                    break;
            }

            i += piece.Text.Length;
        }

        return new List<TextHighlighter>
        {
            insert,
            delete
        };
    }

    internal static void Add(TextHighlighter highlighter, int start, int length)
    {
        if (highlighter.Ranges.Count > 0)
        {
            var last = highlighter.Ranges.Last();
            var end = last.StartIndex + last.Length;
            if (start == end)
            {
                start = last.StartIndex;
                length += last.Length;
                highlighter.Ranges.Remove(last);
            }
        }

        highlighter.Ranges.Add(new TextRange(start, length));
    }

    public static async Task<FileInfo> SelectFileAsync(Window window)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker
        {
            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary,
        };
        try
        {
            if (window != null) WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
            picker.FileTypeFilter.Add("*");
            var file = await picker.PickSingleFileAsync();
            if (file != null) return Trivial.IO.FileSystemInfoUtility.TryGetFileInfo(file.Path);
        }
        catch (ArgumentException)
        {
        }
        catch (IOException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (SecurityException)
        {
        }
        catch (NotSupportedException)
        {
        }
        catch (ExternalException)
        {
        }

        return null;
    }

    public static async Task<string> TryGetFileTextAsync(Window window, Action<Exception> errorHandler = null)
    {
        try
        {
            var file = await SelectFileAsync(window);
            if (file == null || !file.Exists) return null;
            return await File.ReadAllTextAsync(file.FullName);
        }
        catch (ArgumentException ex)
        {
            errorHandler(ex);
        }
        catch (IOException ex)
        {
            errorHandler(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            errorHandler(ex);
        }
        catch (SecurityException ex)
        {
            errorHandler(ex);
        }
        catch (InvalidOperationException ex )
        {
            errorHandler(ex);
        }
        catch (NotSupportedException ex)
        {
            errorHandler(ex);
        }
        catch (ExternalException ex)
        {
            errorHandler(ex);
        }
        catch (OutOfMemoryException)
        {
            throw;
        }
        catch (Exception ex)
        {
            errorHandler(ex);
            throw;
        }

        return null;
    }
}

/// <summary>
/// The base view model for diff text.
/// </summary>
internal abstract class BaseDiffTextViewModel
{
    /// <summary>
    /// Get or set the line number.
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// Gets a value indicating whether the line is unchanged.
    /// </summary>
    public abstract bool IsUnchanged { get; }

    /// <summary>
    /// Gets a value indicating whether the current line is null.
    /// </summary>
    public abstract bool IsNullLine { get; }

    /// <summary>
    /// Returns a value indicating whether a specified substring occurs within the text in this view model.
    /// </summary>
    /// <param name="q">The string to seek.</param>
    /// <returns>true if the value parameter occurs within the text in this view model; otherwise, false.</returns>
    public abstract bool Contains(string q);
}

/// <summary>
/// The diff text view model of split mode.
/// </summary>
internal class DiffTextViewModel : BaseDiffTextViewModel
{
    public DiffTextViewModel()
    {
    }

    public DiffTextViewModel(int number, DiffPiece left, DiffPiece right)
        : this(number, left, right, null)
    {
    }

    public DiffTextViewModel(int number, DiffPiece left, DiffPiece right, DiffTextViewReference reference)
    {
        LineNumber = number;
        Left = left;
        Right = right;
        Reference = reference;
    }

    public DiffPiece Left { get; private set; }

    public DiffPiece Right { get; private set; }

    public DiffTextViewReference Reference { get; private set; }

    public string LeftText => Left?.Text;

    public string RightText => Right?.Text;

    /// <inheritdoc />
    public override bool IsUnchanged => Right?.Type == ChangeType.Unchanged;

    /// <inheritdoc />
    public override bool IsNullLine => Right is null;

    public IEnumerable<TextHighlighter> GetLeftHighlighter()
        => InternalUtilities.GetTextHighlighter(Left?.SubPieces, ChangeType.Deleted, Reference?.Element?.Foreground);

    public IEnumerable<TextHighlighter> GetRightHighlighter()
        => InternalUtilities.GetTextHighlighter(Right?.SubPieces, ChangeType.Inserted, Reference?.Element?.Foreground);

    /// <inheritdoc />
    public override bool Contains(string q)
    {
        if (string.IsNullOrEmpty(q)) return false;
        var v = Right?.Text;
        if (v != null && v.Contains(q)) return true;
        v = Left?.Text;
        if (v != null && v.Contains(q)) return true;
        return false;
    }
}

/// <summary>
/// The diff text view model of unified mode.
/// </summary>
internal class InlineDiffTextViewModel : BaseDiffTextViewModel
{
    public InlineDiffTextViewModel()
    {
    }

    public InlineDiffTextViewModel(int number, DiffPiece line)
        : this(number, line, null)
    {
    }

    public InlineDiffTextViewModel(int number, DiffPiece line, DiffTextViewReference reference)
    {
        LineNumber = number;
        Line = line;
        Reference = reference;
    }

    public DiffPiece Line { get; private set; }

    public string Text => Line?.Text;

    public int? Position => Line?.Position;

    public DiffTextViewReference Reference { get; private set; }

    /// <inheritdoc />
    public override bool IsUnchanged => Line?.Type == ChangeType.Unchanged;

    /// <inheritdoc />
    public override bool IsNullLine => Line is null;

    public IEnumerable<TextHighlighter> GetTextHighlighter()
        => InternalUtilities.GetTextHighlighter(Line?.SubPieces, ChangeType.Deleted, Reference?.Element?.Foreground);

    /// <inheritdoc />
    public override bool Contains(string q)
    {
        if (string.IsNullOrEmpty(q)) return false;
        var v = Line?.Text;
        return v != null && v.Contains(q);
    }
}

internal class DiffTextViewReference(DiffTextView element)
{
    public DiffTextView Element { get; set; } = element;
}
