
# Kiyote.Imaging

## Overview
Provides simple reduced-dependency code for reading and writing images to disk.

## Getting Started

### Using DI

```csharp

public static void Main(
    string[] args
) {
    var services = new ServiceCollection();
    services.AddPngImaging();
    var serviceProvider = services.BuildServiceProvider();
    var myClass = serviceProvider.GetRequiredService<MyClass>();
    myClass.CreateImage( 800, 600 );
}


public sealed class MyClass {

    private readonly IBufferFactory _bufferFactory;
    private readonly IImageWriter _imageWriter;

    public MyClass(
        IBufferFactory bufferFactory,
        IImageWriter imageWriter
    ) {
        _bufferFactory = bufferFactory;
        _imageWriter = imageWriter;
    }

    public void CreateImage(
        int width,
        int height
    ) {
        IBuffer<uint> pixels = _bufferFactory.Create( width, height, 0U );

        // Set the pixels in the buffer to some color values

        _imageWriter.Write("image.png", pixels);
    }
```
