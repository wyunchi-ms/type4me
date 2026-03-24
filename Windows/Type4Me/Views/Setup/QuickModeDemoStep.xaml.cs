using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Type4Me.Localization;

namespace Type4Me.Views.Setup;

/// <summary>
/// Quick Mode demo step — shows an animated floating bar simulating voice input.
/// Cycles through: preparing → recording (text appears) → done → hidden → repeat.
/// </summary>
public partial class QuickModeDemoStep : UserControl
{
    private DispatcherTimer? _timer;
    private int _phase; // 0=hidden, 1=preparing, 2=recording, 3=done
    private int _charIndex;
    private readonly string _demoText;

    public QuickModeDemoStep()
    {
        InitializeComponent();

        TitleText.Text = Loc.L("快速模式", "Quick Mode");
        DescText.Text = Loc.L(
            "按下快捷键后说话，语音直接转为文字输入。适合快速输入短句。",
            "Press the hotkey and speak. Speech is directly converted to text input. Great for quick sentences.");

        _demoText = Loc.L(
            "三点开会讨论新版本的发布计划",
            "Meeting at three to discuss the new release plan");

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _phase = 0;
        _charIndex = 0;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
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
            case 0: // Hidden → Preparing
                _phase = 1;
                _charIndex = 0;
                DemoText.Text = Loc.L("准备中...", "Preparing...");
                StatusDot.Fill = new SolidColorBrush(Color.FromRgb(212, 145, 61));
                AnimateBarIn();
                _timer!.Interval = TimeSpan.FromMilliseconds(800);
                break;

            case 1: // Preparing → Recording
                _phase = 2;
                DemoText.Text = "";
                StatusDot.Fill = new SolidColorBrush(Color.FromRgb(222, 97, 77));
                _timer!.Interval = TimeSpan.FromMilliseconds(60);
                break;

            case 2: // Recording — type text
                if (_charIndex < _demoText.Length)
                {
                    _charIndex++;
                    DemoText.Text = _demoText[.._charIndex];
                }
                else
                {
                    _phase = 3;
                    StatusDot.Fill = new SolidColorBrush(Color.FromRgb(107, 179, 107));
                    DemoText.Text = Loc.L("完成 ✓", "Done ✓");
                    _timer!.Interval = TimeSpan.FromMilliseconds(2000);
                }
                break;

            case 3: // Done → Hidden
                _phase = 0;
                AnimateBarOut();
                _timer!.Interval = TimeSpan.FromMilliseconds(1500);
                break;
        }
    }

    private void AnimateBarIn()
    {
        var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        DemoBar.BeginAnimation(OpacityProperty, anim);
    }

    private void AnimateBarOut()
    {
        var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
        };
        DemoBar.BeginAnimation(OpacityProperty, anim);
    }
}
