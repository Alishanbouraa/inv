// GridLengthAnimation.cs
using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace QuickTechSystems.WPF.Helpers
{
    public class GridLengthAnimation : AnimationTimeline
    {
        public override Type TargetPropertyType => typeof(GridLength);

        protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

        public GridLength From
        {
            get => (GridLength)GetValue(FromProperty);
            set => SetValue(FromProperty, value);
        }

        public GridLength To
        {
            get => (GridLength)GetValue(ToProperty);
            set => SetValue(ToProperty, value);
        }

        public static readonly DependencyProperty FromProperty =
            DependencyProperty.Register("From", typeof(GridLength), typeof(GridLengthAnimation));

        public static readonly DependencyProperty ToProperty =
            DependencyProperty.Register("To", typeof(GridLength), typeof(GridLengthAnimation));

        public override object GetCurrentValue(object defaultOriginValue,
            object defaultDestinationValue,
            AnimationClock animationClock)
        {
            if (animationClock.CurrentProgress == null)
                return new GridLength(0);

            double fromValue = From.Value;
            double toValue = To.Value;
            return new GridLength((toValue - fromValue) * animationClock.CurrentProgress.Value + fromValue,
                GridUnitType.Pixel);
        }
    }
}