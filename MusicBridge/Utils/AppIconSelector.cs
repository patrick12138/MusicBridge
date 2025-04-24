using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MusicBridge.Utils
{
    /// <summary>
    /// 管理应用图标的选择状态
    /// </summary>
    public class AppIconSelector
    {
        private readonly List<Border> _appIcons = new List<Border>();
        private readonly SolidColorBrush _selectionColor = new SolidColorBrush(Color.FromRgb(0, 120, 215)); // 选中状态颜色
        private readonly SolidColorBrush _normalBorderColor = new SolidColorBrush(Colors.Transparent); // 正常状态颜色
        private int _selectedIndex = -1;

        /// <summary>
        /// 当前选中的应用索引
        /// </summary>
        public int SelectedIndex => _selectedIndex;

        /// <summary>
        /// 注册应用图标到选择器中
        /// </summary>
        public void RegisterAppIcon(Border appIcon)
        {
            if (!_appIcons.Contains(appIcon))
            {
                _appIcons.Add(appIcon);
                
                // 注意：图标结构已简化，不再需要添加选中指示器
                // 现在直接通过边框颜色和tooltip展示选中状态
            }
        }

        /// <summary>
        /// 选择指定索引的应用图标
        /// </summary>
        public void SelectAppIcon(int index)
        {
            // 有效性检查
            if (index < 0 || index >= _appIcons.Count) return;
            
            // 取消当前选中项
            if (_selectedIndex >= 0 && _selectedIndex < _appIcons.Count)
            {
                Border currentIcon = _appIcons[_selectedIndex];
                currentIcon.BorderBrush = _normalBorderColor;
            }
            
            // 设置新选中项
            _selectedIndex = index;
            Border newSelectedIcon = _appIcons[_selectedIndex];
            newSelectedIcon.BorderBrush = _selectionColor;
            
            // 更新Tooltip以显示"已选择"状态
            string originalTooltip = newSelectedIcon.ToolTip?.ToString() ?? "";
            if (!originalTooltip.Contains("(已选择)"))
            {
                newSelectedIcon.ToolTip = $"{originalTooltip} (已选择)";
            }
        }

        /// <summary>
        /// 清除选择
        /// </summary>
        public void ClearSelection()
        {
            if (_selectedIndex >= 0 && _selectedIndex < _appIcons.Count)
            {
                Border currentIcon = _appIcons[_selectedIndex];
                currentIcon.BorderBrush = _normalBorderColor;
                
                // 恢复原始Tooltip
                string originalTooltip = currentIcon.ToolTip?.ToString() ?? "";
                if (originalTooltip.Contains("(已选择)"))
                {
                    currentIcon.ToolTip = originalTooltip.Replace(" (已选择)", "");
                }
            }
            
            _selectedIndex = -1;
        }
    }
}