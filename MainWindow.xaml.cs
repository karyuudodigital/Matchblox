using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Clipboard = System.Windows.Clipboard;
using Color = System.Drawing.Color;
using Resx = DiamondSword.Resources.Resources;


namespace DiamondSword
{
    public partial class MainWindow : Window
    {
        private struct Block
        {
            public string Name;
            public int R, G, B;
            public double A;
            public double RgbRange;
            public ImageSource Icon;
            public readonly string DisplayName => System.IO.Path.GetFileNameWithoutExtension(Name).Replace("_", " ");
        }

        private readonly List<Block> blocks = [];
        private readonly string baseDir =
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DiamondSword");

        private readonly string texturesDir;
        private readonly string blockCfg;
        private readonly bool textChangedLock = true;

        public MainWindow()
        {

            //Initialize paths
            texturesDir = Path.Combine(baseDir, "textures");
            blockCfg = Path.Combine(baseDir, "blocks.cfg");

            Directory.CreateDirectory(baseDir);
            Directory.CreateDirectory(texturesDir);


            //Thread.CurrentThread.CurrentUICulture = new CultureInfo("ja-JP");

            InitializeComponent();

            // Localized window title
            this.Title = Resx.Window_Title_BlockMatcher;

            // Initialize UI defaults
            SliderR.Value = 0; SliderG.Value = 0; SliderB.Value = 0;
            TxtR.Text = "0"; TxtG.Text = "0"; TxtB.Text = "0";
            SliderAlpha.Value = 1.0; TxtAlpha.Text = "1.00";
            TxtAmount.Text = "12";

            // Ensure textures folder exists
            Directory.CreateDirectory(texturesDir);

            // Load existing blocks.cfg if present
            if (File.Exists(blockCfg))
            {
                ParseBlockData();
            }
            else
            {
                System.Windows.MessageBox.Show(
                    Resx.Message_NoBlocksCfg,
                    Resx.Message_Notice_Title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            DisplayByRGB();
            UpdateColorPreview();
            textChangedLock = false;
        }

        // --- UI handlers ---

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = Resx.Dialog_SelectFolder,
                ShowNewFolderButton = false
            };

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ImportTexturesFromFolder(dlg.SelectedPath);
                ParseBlockData();

                DisplayByRGB();
            }
        }

        private void BtnDisplay_Click(object sender, RoutedEventArgs e) => DisplayByRGB();

        private void BtnApplyAmount_Click(object sender, RoutedEventArgs e)
        {
            if (!textChangedLock)
            {
                if (!int.TryParse(TxtAmount.Text, out int amt) || amt < 1)
                {
                    System.Windows.MessageBox.Show(
                        Resx.Message_InvalidAmount,
                        Resx.Message_Notice_Title,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    TxtAmount.Text = "16";
                }

                DisplayByRGB();
            }
        }

        private void RgbSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            TxtR.Text = ((int)SliderR.Value).ToString();
            TxtG.Text = ((int)SliderG.Value).ToString();
            TxtB.Text = ((int)SliderB.Value).ToString();
            UpdateColorPreview();
            DisplayByRGB();
        }

        private void AlphaSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            TxtAlpha.Text = SliderAlpha.Value.ToString("0.00");
            DisplayByRGB();
        }

        private void BtnPickColor_Click(object sender, RoutedEventArgs e)
        {
            using var cd = new ColorDialog
            {
                Color = Color.FromArgb(255, (int)SliderR.Value, (int)SliderG.Value, (int)SliderB.Value)
            };
            if (cd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SliderR.Value = cd.Color.R;
                SliderG.Value = cd.Color.G;
                SliderB.Value = cd.Color.B;
            }
        }

        private void ListBlocks_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ListBlocks.SelectedItem is BlockViewModel vm)
            {
                string blockName = vm.OriginalName!.Replace(" ", "_");
                string copiedText = $"/give @p {blockName} 10";
                Clipboard.SetText(copiedText);

                System.Windows.MessageBox.Show(
                    string.Format(Resx.Message_Copied, blockName),
                    Resx.Message_Copied_Title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        // --- Core logic ---

        private void UpdateColorPreview()
        {
            var color = System.Windows.Media.Color.FromRgb(
                (byte)SliderR.Value,
                (byte)SliderG.Value,
                (byte)SliderB.Value);
            ColorPreview.Background = new SolidColorBrush(color);
        }

        private void ImportTexturesFromFolder(string sourceFolder)
        {
            Directory.CreateDirectory(texturesDir);
            var files = Directory.EnumerateFiles(sourceFolder, "*.png", SearchOption.TopDirectoryOnly).ToList();

            if (files.Count == 0)
            {
                System.Windows.MessageBox.Show(
                    Resx.Message_NoPngFound,
                    Resx.Message_Error_Title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            blocks.Clear();
            if (File.Exists(blockCfg)) File.Delete(blockCfg);

            using var writer = new StreamWriter(blockCfg);
            foreach (var f in files)
            {
                try
                {
                    var dest = Path.Combine(texturesDir, Path.GetFileName(f));
                    File.Copy(f, dest, overwrite: true);

                    using var pix = new Bitmap(dest);
                    long r = 0, g = 0, b = 0;
                    int w = pix.Width, h = pix.Height;
                    int total = w * h, alphaCount = 0;

                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            var c = pix.GetPixel(x, y);
                            r += c.R;
                            g += c.G;
                            b += c.B;
                            if (c.A > 0) alphaCount++;
                        }

                    int avgR = (int)(r / total);
                    int avgG = (int)(g / total);
                    int avgB = (int)(b / total);
                    double aFrac = (double)alphaCount / total;

                    writer.WriteLine(Path.GetFileName(dest));
                    writer.WriteLine(avgR);
                    writer.WriteLine(avgG);
                    writer.WriteLine(avgB);
                    writer.WriteLine(aFrac.ToString("G9"));

                    blocks.Add(new Block()
                    {
                        Name = Path.GetFileName(dest),
                        R = avgR,
                        G = avgG,
                        B = avgB,
                        A = aFrac,
                        Icon = LoadBitmapImage(dest)!
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed processing {f}: {ex.Message}");
                }
            }
        }

        private void ParseBlockData()
        {
            blocks.Clear();
            if (!File.Exists(blockCfg)) return;

            var lines = File.ReadAllLines(blockCfg);
            for (int i = 0; i + 4 < lines.Length; i += 5)
            {
                try
                {
                    string name = lines[i].Trim();
                    int r = int.Parse(lines[i + 1]);
                    int g = int.Parse(lines[i + 2]);
                    int b = int.Parse(lines[i + 3]);
                    double a = double.Parse(lines[i + 4]);
                    var path = Path.Combine(texturesDir, name);
                    var icon = File.Exists(path) ? LoadBitmapImage(path) : null;

                    var blk = new Block()
                    {
                        Name = name,
                        R = r,
                        G = g,
                        B = b,
                        A = a,
                        Icon = icon!
                    };
                    blocks.Add(blk);
                }
                catch { }
            }
        }

        private void DisplayByRGB()
        {
            ListBlocks.ItemsSource = null;
            if (blocks.Count == 0) return;

            int r1 = (int)SliderR.Value, g1 = (int)SliderG.Value, b1 = (int)SliderB.Value;
            double alphaFilter = SliderAlpha.Value;
            if (!int.TryParse(TxtAmount.Text, out int amount)) amount = 16;
            amount = Math.Max(1, amount);

            for (int i = 0; i < blocks.Count; i++)
            {
                var blk = blocks[i];
                double dist = Math.Sqrt(Math.Pow(blk.R - r1, 2) + Math.Pow(blk.G - g1, 2) + Math.Pow(blk.B - b1, 2));
                blk.RgbRange = dist;
                blocks[i] = blk;
            }

            var filtered = blocks.Where(b => b.A >= alphaFilter)
                                 .OrderBy(b => b.RgbRange)
                                 .Take(amount)
                                 .ToList();

            var vms = filtered.Select(b => new BlockViewModel()
            {
                Icon = b.Icon,
                DisplayName = b.DisplayName,
                OriginalName = b.Name
            }).ToList();

            ListBlocks.ItemsSource = vms;
        }

        private static RenderTargetBitmap? LoadBitmapImage(string path)
        {
            try
            {
                var bitmap = new BitmapImage();
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = fs;
                    bitmap.EndInit();
                }
                bitmap.Freeze();

                int width = bitmap.PixelWidth;
                int height = bitmap.PixelHeight;

                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    RenderOptions.SetBitmapScalingMode(dv, BitmapScalingMode.NearestNeighbor);
                    dc.DrawImage(bitmap, new Rect(0, 0, width, height));
                }

                var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(dv);
                rtb.Freeze();

                return rtb;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Image load failed: {ex.Message}");
                return null;
            }
        }

        private class BlockViewModel
        {
            public ImageSource ?Icon { get; set; }
            public string ?DisplayName { get; set; }
            public string ?OriginalName { get; set; }
        }
    }
}
