using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Type4Me.Localization;

namespace Type4Me.Views.Setup;

/// <summary>
/// Custom Mode demo step — shows a typewriter animation demonstrating
/// raw speech → LLM-processed output.
/// </summary>
public partial class CustomModeDemoStep : UserControl
{
    private DispatcherTimer? _timer;
    private int _phase; // 0=typing raw, 1=showing processed, 2=typing processed, 3=hold, 4=reset
    private int _charIndex;

    private readonly string _rawDemo;
    private readonly string _processedDemo;

    public CustomModeDemoStep()
    {
        InitializeComponent();

        TitleText.Text = Loc.L("自定义模式", "Custom Mode");
        DescText.Text = Loc.L(
            "语音识别后，通过 LLM 进一步处理文本。可用于翻译、润色、格式化等。",
            "After speech recognition, the text is further processed by an LLM. Useful for translation, polishing, formatting, etc.");

        RawLabel.Text = Loc.L("语音识别原文", "Raw Speech");
        ProcessedLabel.Text = Loc.L("AI 处理结果", "AI Processed");

        _rawDemo = Loc.L(
            "那个就是英伟达最近的股票走势其实还不错，因为他们发布了新的GPU架构，然后市场反应还是比较积极的",
            "So NVIDIA stock has been doing pretty well lately because they released their new GPU architecture and the market reaction was pretty positive");

        _processedDemo = Loc.L(
            "英伟达近期股票走势表现良好。受益于新 GPU 架构的发布，市场反应积极。",
            "NVIDIA stock has performed well recently, driven by positive market reaction to the release of their new GPU architecture.");

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _phase = 0;
        _charIndex = 0;
        RawText.Text = "";
        ProcessedText.Text = "";
        ProcessedCard.Opacity = 0;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer?.Stop();
        _timer = null;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        switch (_phase)
        {
            case 0: // Type raw text
                if (_charIndex < _rawDemo.Length)
                {
                    _charIndex++;
                    RawText.Text = _rawDemo[.._charIndex];
                }
                else
                {
                    _phase = 1;
                    _charIndex = 0;
                    _timer!.Interval = TimeSpan.FromMilliseconds(500);
                }
                break;

            case 1: // Show processed card
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
                ProcessedCard.BeginAnimation(OpacityProperty, fadeIn);
                _phase = 2;
                _timer!.Interval = TimeSpan.FromMilliseconds(30);
                break;

            case 2: // Type processed text
                if (_charIndex < _processedDemo.Length)
                {
                    _charIndex++;
                    ProcessedText.Text = _processedDemo[.._charIndex];
                }
                else
                {
                    _phase = 3;
                    _timer!.Interval = TimeSpan.FromMilliseconds(3000);
                }
                break;

            case 3: // Hold
                _phase = 4;
                _timer!.Interval = TimeSpan.FromMilliseconds(500);
                break;

            case 4: // Reset
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
                ProcessedCard.BeginAnimation(OpacityProperty, fadeOut);
                _phase = 0;
                _charIndex = 0;
                RawText.Text = "";
                ProcessedText.Text = "";
                _timer!.Interval = TimeSpan.FromMilliseconds(1000);
                break;
        }
    }
}
