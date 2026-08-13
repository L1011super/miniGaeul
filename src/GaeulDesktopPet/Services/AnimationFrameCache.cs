using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GaeulDesktopPet.Services;

public sealed class AnimationFrameCache
{
    private readonly int _capacity;
    private readonly Dictionary<string, LinkedListNode<Entry>> _map = [];
    private readonly LinkedList<Entry> _lru = [];
    private readonly Dictionary<BitmapSource, AlphaPlane> _alphaPlanes = [];

    public AnimationFrameCache(int capacity = 180)
    {
        _capacity = capacity;
    }

    public BitmapSource Get(string path)
    {
        if (_map.TryGetValue(path, out var node))
        {
            _lru.Remove(node);
            _lru.AddFirst(node);
            return node.Value.Bitmap;
        }

        var bitmap = Load(path);
        var entry = new Entry(path, bitmap);
        var newNode = new LinkedListNode<Entry>(entry);
        _lru.AddFirst(newNode);
        _map[path] = newNode;
        Trim();
        return bitmap;
    }

    public void Clear()
    {
        _map.Clear();
        _lru.Clear();
        _alphaPlanes.Clear();
    }

    public byte GetAlpha(BitmapSource bitmap, int x, int y)
    {
        if (x < 0 || y < 0 || x >= bitmap.PixelWidth || y >= bitmap.PixelHeight) return 0;
        if (!_alphaPlanes.TryGetValue(bitmap, out var plane))
        {
            plane = CreateAlphaPlane(bitmap);
            _alphaPlanes[bitmap] = plane;
        }

        return plane.Values[y * plane.Width + x];
    }

    private static AlphaPlane CreateAlphaPlane(BitmapSource bitmap)
    {
        var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);

        var alpha = new byte[converted.PixelWidth * converted.PixelHeight];
        for (var index = 0; index < alpha.Length; index++)
        {
            alpha[index] = pixels[index * 4 + 3];
        }

        return new AlphaPlane(alpha, converted.PixelWidth);
    }

    private static BitmapImage Load(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        if (bitmap.CanFreeze) bitmap.Freeze();
        return bitmap;
    }

    private void Trim()
    {
        while (_map.Count > _capacity && _lru.Last is not null)
        {
            var removed = _lru.Last.Value;
            _map.Remove(removed.Path);
            _alphaPlanes.Remove(removed.Bitmap);
            _lru.RemoveLast();
        }
    }

    private sealed record Entry(string Path, BitmapSource Bitmap);
    private sealed record AlphaPlane(byte[] Values, int Width);
}
