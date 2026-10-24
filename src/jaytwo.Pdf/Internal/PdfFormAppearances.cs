#pragma warning disable SA1201, SA1202, SA1204
using System;
using System.Collections.Generic;
using jaytwo.Pdf.AcroForm;

namespace jaytwo.Pdf.Internal;

// Generates widget appearance streams, and tracks the shared Helvetica font and the /NeedAppearances flag.
internal sealed class PdfFormAppearances
{
    private readonly PdfObjectStore _store;
    private readonly PdfFieldTree _fields;

    internal PdfFormAppearances(PdfObjectStore store, PdfFieldTree fields)
    {
        _store = store;
        _fields = fields;
    }

    internal int HelvId { get; set; }

    internal bool StickyNeedAppearances { get; set; }

    internal void SetNeedAppearances(bool value)
    {
        PdfObject.Dict acro = _fields.EnsureAcroForm();
        acro.Set("NeedAppearances", PdfObject.Bool.From(value));
        if (value)
        {
            StickyNeedAppearances = true;
        }
    }

    internal int EnsureHelv()
    {
        if (HelvId != 0)
        {
            return HelvId;
        }

        PdfObject.Dict acro = _fields.EnsureAcroForm();
        if (_store.ResolveMaybe(acro.Get("DR")) is PdfObject.Dict dr
            && _store.ResolveMaybe(dr.Get("Font")) is PdfObject.Dict fonts
            && fonts.Get("Helv") is PdfObject.Ref existing
            && _store.IsLive(existing))
        {
            HelvId = existing.ObjectId;
            return HelvId;
        }

        var font = new PdfObject.Dict();
        font.Set("Type", new PdfObject.Name("Font"));
        font.Set("Subtype", new PdfObject.Name("Type1"));
        font.Set("BaseFont", new PdfObject.Name("Helvetica"));
        font.Set("Encoding", new PdfObject.Name("WinAnsiEncoding"));
        HelvId = _store.Allocate(font);
        PdfObject.Dict drEdit = acro.Get("DR") is PdfObject.Ref drRef && _store.IsLive(drRef)
            ? (PdfObject.Dict)_store.EditAny(drRef.ObjectId)
            : GetOrCreateDict(acro, "DR");
        PdfObject.Dict fontEdit = drEdit.Get("Font") is PdfObject.Ref fontRef && _store.IsLive(fontRef)
            ? (PdfObject.Dict)_store.EditAny(fontRef.ObjectId)
            : GetOrCreateDict(drEdit, "Font");
        fontEdit.Set("Helv", new PdfObject.Ref(HelvId, 0));
        return HelvId;
    }

    private static PdfObject.Dict GetOrCreateDict(PdfObject.Dict parent, string key)
    {
        if (parent.Get(key) is PdfObject.Dict dict)
        {
            return dict;
        }

        var created = new PdfObject.Dict();
        parent.Set(key, created);
        return created;
    }

    internal bool TryWriteCheckBoxAppearances(List<FieldSlot> matches)
    {
        foreach (FieldSlot slot in matches)
        {
            if (Math.Abs(slot.Rect.Width) <= 0 || Math.Abs(slot.Rect.Height) <= 0)
            {
                return false;
            }
        }

        foreach (FieldSlot slot in matches)
        {
            double width = Math.Abs(slot.Rect.Width);
            double height = Math.Abs(slot.Rect.Height);
            WriteStateAppearances(
                slot.WidgetObjectId,
                "Yes",
                AppearanceBuilder.CheckBox(true, width, height),
                AppearanceBuilder.CheckBox(false, width, height),
                width,
                height);
        }

        return true;
    }

    internal bool TryWriteRadioAppearances(List<FieldSlot> matches, string exportValue)
    {
        if (!IsPdfName(exportValue))
        {
            return false;
        }

        foreach (FieldSlot slot in matches)
        {
            if (Math.Abs(slot.Rect.Width) <= 0 || Math.Abs(slot.Rect.Height) <= 0)
            {
                return false;
            }
        }

        foreach (FieldSlot slot in matches)
        {
            double width = Math.Abs(slot.Rect.Width);
            double height = Math.Abs(slot.Rect.Height);
            WriteStateAppearances(
                slot.WidgetObjectId,
                exportValue,
                AppearanceBuilder.Radio(true, width, height),
                AppearanceBuilder.Radio(false, width, height),
                width,
                height);
        }

        return true;
    }

    internal bool TryWriteChoiceAppearance(List<FieldSlot> matches, IReadOnlyList<string> values)
    {
        var lines = new List<string>();
        foreach (string value in values)
        {
            string display = value;
            foreach (PdfChoiceOption option in matches[0].Options)
            {
                if (option.Value == value)
                {
                    display = option.Display;
                    break;
                }
            }

            if (!PdfTextCodec.CanEncodeWinAnsi(display))
            {
                return false;
            }

            lines.Add(display);
        }

        foreach (FieldSlot slot in matches)
        {
            if (Math.Abs(slot.Rect.Width) <= 0 || Math.Abs(slot.Rect.Height) <= 0)
            {
                return false;
            }
        }

        string text = string.Join("\n", lines);
        int fontId = EnsureHelv();
        foreach (FieldSlot slot in matches)
        {
            var appearance = AppearanceBuilder.ParseDefaultAppearance(slot.DefaultAppearance);
            var box = AppearanceBox(slot.WidgetObjectId);
            byte[] bytes = AppearanceBuilder.Build(
                text,
                Math.Abs(slot.Rect.Width),
                Math.Abs(slot.Rect.Height),
                appearance.Size,
                slot.Align,
                appearance.Red,
                appearance.Green,
                appearance.Blue,
                lines.Count > 1 || slot.Multiline,
                false,
                box.Background,
                box.Border,
                box.BorderWidth);
            WriteNormalAppearance(slot.WidgetObjectId, AllocateHelvAppearance(bytes, slot, fontId));
        }

        if (!StickyNeedAppearances)
        {
            SetNeedAppearances(false);
        }

        return true;
    }

    private void WriteStateAppearances(int widgetId, string onName, byte[] onBytes, byte[] offBytes, double width, double height)
    {
        int onId = AllocateFormXObject(onBytes, width, height, false, 0);
        int offId = AllocateFormXObject(offBytes, width, height, false, 0);
        PdfObject.Dict widget = (PdfObject.Dict)_store.EditAny(widgetId);
        var states = new PdfObject.Dict();
        states.Set(onName, new PdfObject.Ref(onId, 0));
        states.Set("Off", new PdfObject.Ref(offId, 0));
        var appearance = new PdfObject.Dict();
        appearance.Set("N", states);
        widget.Set("AP", appearance);
    }

    private void WriteNormalAppearance(int widgetId, int appearanceId)
    {
        PdfObject.Dict widget = (PdfObject.Dict)_store.EditAny(widgetId);
        var normal = new PdfObject.Dict();
        normal.Set("N", new PdfObject.Ref(appearanceId, 0));
        widget.Set("AP", normal);
    }

    private int AllocateHelvAppearance(byte[] bytes, FieldSlot slot, int fontId)
    {
        return AllocateFormXObject(bytes, Math.Abs(slot.Rect.Width), Math.Abs(slot.Rect.Height), true, fontId);
    }

    internal (double[]? Background, double[]? Border, double BorderWidth) AppearanceBox(int widgetId)
    {
        PdfObject.Dict widget = (PdfObject.Dict)_store.EditAny(widgetId);
        if (_store.ResolveMaybe(widget.Get("MK")) is not PdfObject.Dict appearance)
        {
            return (null, null, 0);
        }

        double[]? background = AppearanceColor(appearance.Get("BG"));
        double[]? border = AppearanceColor(appearance.Get("BC"));
        double borderWidth = border == null ? 0 : 1;
        if (_store.ResolveMaybe(widget.Get("BS")) is PdfObject.Dict borderStyle)
        {
            string? style = PdfValues.NameOf(_store.ResolveMaybe(borderStyle.Get("S")));
            if (style != null && style != "S")
            {
                border = null;
                borderWidth = 0;
            }
            else if (_store.TryNumber(borderStyle.Get("W"), out double width))
            {
                borderWidth = Math.Max(0, width);
            }
        }

        return (background, border, borderWidth);
    }

    private double[]? AppearanceColor(PdfObject? value)
    {
        if (_store.ResolveMaybe(value) is not PdfObject.Arr array || (array.Items.Count != 1 && array.Items.Count != 3 && array.Items.Count != 4))
        {
            return null;
        }

        var color = new double[array.Items.Count];
        for (int i = 0; i < color.Length; i++)
        {
            if (!_store.TryNumber(array.Items[i], out color[i]))
            {
                return null;
            }
        }

        return color;
    }

    private int AllocateFormXObject(byte[] bytes, double width, double height, bool helvetica, int fontId)
    {
        var stream = new PdfObject.Dict();
        stream.Set("Type", new PdfObject.Name("XObject"));
        stream.Set("Subtype", new PdfObject.Name("Form"));
        stream.Set("FormType", PdfObject.Num.FromLong(1));
        stream.Set("BBox", PdfValues.RectArray(new PdfRect(0, 0, width, height)));
        if (helvetica)
        {
            var resources = new PdfObject.Dict();
            var fonts = new PdfObject.Dict();
            fonts.Set("Helv", new PdfObject.Ref(fontId, 0));
            resources.Set("Font", fonts);
            stream.Set("Resources", resources);
        }

        stream.Set("Length", PdfObject.Num.FromLong(bytes.Length));
        stream.Body = new PdfObject.MemoryBody(bytes);
        return _store.Allocate(stream);
    }

    private static bool IsPdfName(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        foreach (char character in value)
        {
            if (character <= 32 || character >= 127 || character is '#' or '/' or '(' or ')' or '<' or '>' or '[' or ']' or '{' or '}' or '%')
            {
                return false;
            }
        }

        return true;
    }
}
