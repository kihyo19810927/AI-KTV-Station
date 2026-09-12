using System.Globalization;
using System.Windows.Data;
using Station.Domain.Models;

namespace Station.Desktop.Converters;

public sealed class QueueStatusTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is QueueItemStatus status
        ? status switch
        {
            QueueItemStatus.Probing => "正在探测媒体",
            QueueItemStatus.ProbeFailed => "媒体探测失败",
            QueueItemStatus.Waiting => "等待播放",
            QueueItemStatus.Preparing => "正在准备播放",
            QueueItemStatus.Playing => "正在播放",
            QueueItemStatus.Paused => "已暂停",
            QueueItemStatus.Completed => "播放完成",
            QueueItemStatus.Skipped => "已跳过",
            QueueItemStatus.Failed => "播放失败",
            _ => status.ToString(),
        }
        : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
