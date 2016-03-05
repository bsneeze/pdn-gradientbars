using PaintDotNet;
using PaintDotNet.Effects;
using PaintDotNet.IndirectUI;
using PaintDotNet.PropertySystem;
using pyrochild.effects.common;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace pyrochild.effects.gradientbars
{
    [PluginSupportInfo(typeof(PluginSupportInfo))]
    class GradientBars : PropertyBasedEffect
    {
        public GradientBars()
            : base(StaticName, new Bitmap(typeof(GradientBars), "icon.png"), SubmenuNames.Render, EffectFlags.Configurable)
        {
        }

        public static string StaticDialogName
        {
            get
            {
                return StaticName + " by pyrochild";
            }
        }

        public static string StaticName
        {
            get
            {
                string name = "Gradient Bars";
#if DEBUG
                name += " BETA";
#endif
                return name;
            }
        }

        // This is so that repetition of the effect with CTRL+F actually shows up differently.
        private byte instanceSeed = unchecked((byte)DateTime.Now.Ticks);

        public enum PropertyNames
        {
            Alignment,
            Scale,
            Width,
            Spacing,
            Skew,
            AA,
            Seed,
            Color1,
            Alpha1,
            Color2,
            Alpha2,
            Angle,
            GammaAdjust,
            Repetition
        }

        public enum RepeatMode
        {
            None,
            Repeat,
            Mirror
        }

        bool GammaAdjust;
        double Alignment;
        double Scale;
        int Width;
        int Spacing;
        double Skew;
        bool AA;
        RepeatMode Repetition;
        byte Seed;
        ColorBgra Color1,
                  Color2;
        double Angle;

        double[] LengthTable;


        protected override PropertyCollection OnCreatePropertyCollection()
        {
            List<Property> props = new List<Property>();

            props.Add(new Int32Property(PropertyNames.Color1, ColorBgra.ToOpaqueInt32(EnvironmentParameters.PrimaryColor.NewAlpha(255)), 0, 0xFFFFFF));
            props.Add(new Int32Property(PropertyNames.Alpha1, EnvironmentParameters.PrimaryColor.A, 0, 255));
            props.Add(new Int32Property(PropertyNames.Color2, ColorBgra.ToOpaqueInt32(EnvironmentParameters.SecondaryColor.NewAlpha(255)), 0, 0xFFFFFF));
            props.Add(new Int32Property(PropertyNames.Alpha2, EnvironmentParameters.SecondaryColor.A, 0, 255));
            props.Add(new BooleanProperty(PropertyNames.GammaAdjust, false));
            props.Add(new Int32Property(PropertyNames.Width, 1, 1, 100));
            props.Add(new Int32Property(PropertyNames.Spacing, 0, 0, 100));
            props.Add(new DoubleProperty(PropertyNames.Angle, 90, 0, 360));
            props.Add(new DoubleProperty(PropertyNames.Skew, 0, -45, 45));
            props.Add(new DoubleProperty(PropertyNames.Scale, 1, 0.01, 10));
            props.Add(new DoubleProperty(PropertyNames.Alignment, 0, 0, 1));
            props.Add(new StaticListChoiceProperty(PropertyNames.Repetition, Enum.GetNames(typeof(RepeatMode))));
            props.Add(new BooleanProperty(PropertyNames.AA, true));
            props.Add(new Int32Property(PropertyNames.Seed, 0, 0, 255));

            return new PropertyCollection(props);
        }

        protected override void OnCustomizeConfigUIWindowProperties(PropertyCollection props)
        {
            props[ControlInfoPropertyNames.WindowTitle].Value = StaticDialogName;
        }

        protected override ControlInfo OnCreateConfigUI(PropertyCollection props)
        {
            ControlInfo configUI = CreateDefaultConfigUI(props);

            configUI.SetPropertyControlType(PropertyNames.Color1, PropertyControlType.ColorWheel);
            configUI.SetPropertyControlValue(PropertyNames.Color1, ControlInfoPropertyNames.DisplayName, "");
            configUI.SetPropertyControlValue(PropertyNames.Alpha1, ControlInfoPropertyNames.DisplayName, "Alpha");
            configUI.SetPropertyControlType(PropertyNames.Color2, PropertyControlType.ColorWheel);
            configUI.SetPropertyControlValue(PropertyNames.Color2, ControlInfoPropertyNames.DisplayName, "");
            configUI.SetPropertyControlValue(PropertyNames.Alpha2, ControlInfoPropertyNames.DisplayName, "Alpha");
            configUI.SetPropertyControlValue(PropertyNames.Alignment, ControlInfoPropertyNames.SliderSmallChange, 0.05);
            configUI.SetPropertyControlValue(PropertyNames.Alignment, ControlInfoPropertyNames.SliderLargeChange, 0.25);
            configUI.SetPropertyControlValue(PropertyNames.Alignment, ControlInfoPropertyNames.UpDownIncrement, 0.01);
            configUI.SetPropertyControlValue(PropertyNames.Scale, ControlInfoPropertyNames.SliderSmallChange, 1.0);
            configUI.SetPropertyControlValue(PropertyNames.Scale, ControlInfoPropertyNames.SliderLargeChange, 2.5);
            configUI.SetPropertyControlValue(PropertyNames.Scale, ControlInfoPropertyNames.UpDownIncrement, 0.1);
            configUI.SetPropertyControlType(PropertyNames.Angle, PropertyControlType.AngleChooser);
            configUI.SetPropertyControlValue(PropertyNames.Angle, ControlInfoPropertyNames.DisplayName, "");
            configUI.SetPropertyControlValue(PropertyNames.AA, ControlInfoPropertyNames.DisplayName, "");
            configUI.SetPropertyControlValue(PropertyNames.AA, ControlInfoPropertyNames.Description, "Anti-alias");
            configUI.SetPropertyControlType(PropertyNames.Seed, PropertyControlType.IncrementButton);
            configUI.SetPropertyControlValue(PropertyNames.Seed, ControlInfoPropertyNames.DisplayName, "");
            configUI.SetPropertyControlValue(PropertyNames.Seed, ControlInfoPropertyNames.ButtonText, "Reseed");
            configUI.SetPropertyControlValue(PropertyNames.GammaAdjust, ControlInfoPropertyNames.DisplayName, "");
            configUI.SetPropertyControlValue(PropertyNames.GammaAdjust, ControlInfoPropertyNames.Description, "Gamma-adjusted color blend");

            return configUI;
        }

        protected override void OnSetRenderInfo(PropertyBasedEffectConfigToken newToken, RenderArgs dstArgs, RenderArgs srcArgs)
        {
            Rectangle selection = EnvironmentParameters.GetSelection(srcArgs.Bounds).GetBoundsInt();

            this.Color1 = ColorBgra.FromOpaqueInt32(newToken.GetProperty<Int32Property>(PropertyNames.Color1).Value);
            this.Color1.A = (byte)newToken.GetProperty<Int32Property>(PropertyNames.Alpha1).Value;
            this.Color2 = ColorBgra.FromOpaqueInt32(newToken.GetProperty<Int32Property>(PropertyNames.Color2).Value);
            this.Color2.A = (byte)newToken.GetProperty<Int32Property>(PropertyNames.Alpha2).Value;
            this.Alignment = newToken.GetProperty<DoubleProperty>(PropertyNames.Alignment).Value;
            this.Scale = newToken.GetProperty<DoubleProperty>(PropertyNames.Scale).Value;
            this.Width = newToken.GetProperty<Int32Property>(PropertyNames.Width).Value;
            this.Spacing = newToken.GetProperty<Int32Property>(PropertyNames.Spacing).Value;
            this.Skew = newToken.GetProperty<DoubleProperty>(PropertyNames.Skew).Value * Math.PI / 180.0;
            this.Angle = newToken.GetProperty<DoubleProperty>(PropertyNames.Angle).Value * Math.PI / 180.0;
            this.AA = newToken.GetProperty<BooleanProperty>(PropertyNames.AA).Value;
            this.Repetition = (RepeatMode)Enum.Parse(typeof(RepeatMode), (string)newToken.GetProperty<StaticListChoiceProperty>(PropertyNames.Repetition).Value);
            this.Seed = (byte)(newToken.GetProperty<Int32Property>(PropertyNames.Seed).Value ^ instanceSeed);
            this.GammaAdjust = newToken.GetProperty<BooleanProperty>(PropertyNames.GammaAdjust).Value;

            Random rand = new Random(this.Seed);
            LengthTable = new double[Math.Max(selection.Width, selection.Height) * 2];
            for (int i = 0; i < LengthTable.Length; i += this.Width + this.Spacing)
            {
                double val = rand.NextDouble() * this.Scale;
                for (int j = 0; j < this.Width + this.Spacing && j + i < LengthTable.Length; j++)
                {
                    if (j < this.Width)
                    {
                        LengthTable[i + j] = val;
                    }
                }
            }
            base.OnSetRenderInfo(newToken, dstArgs, srcArgs);
        }

        protected unsafe override void OnRender(Rectangle[] renderRects, int startIndex, int length)
        {
            Rectangle selection = EnvironmentParameters.GetSelection(SrcArgs.Bounds).GetBoundsInt();

            byte quadrant;
            if (Angle >= 0 && Angle < Math.PI / 2) quadrant = 1;
            else if (Angle >= Math.PI / 2 && Angle < Math.PI) quadrant = 2;
            else if (Angle >= Math.PI && Angle < Math.PI * 1.5) quadrant = 3;
            else quadrant = 4;

            double maxDistance = Math.Min(Math.Abs(selection.Width / Math.Cos(Angle)), Math.Abs(selection.Height / Math.Sin(Angle)));

            for (int i = startIndex; i < startIndex + length; i++)
            {
                Rectangle rect = renderRects[i];
                for (int y = rect.Top; y < rect.Bottom; y++)
                {
                    ColorBgra* ptr = DstArgs.Surface.GetPointAddressUnchecked(rect.Left, y);
                    for (int x = rect.Left; x < rect.Right; x++)
                    {
                        double distance = 0;
                        double blendFactor;

                        switch (quadrant)
                        {
                            case 1:
                                distance = (selection.Height - y + selection.Top) * Math.Sin(Angle) + (x - selection.Left) * Math.Cos(Angle);
                                break;

                            case 2:
                                distance = (selection.Height - y + selection.Top) * Math.Sin(Angle) - (selection.Width - x + selection.Left) * Math.Cos(Angle);
                                break;

                            case 3:
                                distance = (selection.Top - y) * Math.Sin(Angle) - (selection.Width - x + selection.Left) * Math.Cos(Angle);
                                break;

                            case 4:
                                distance = (selection.Top - y) * Math.Sin(Angle) + (x - selection.Left) * Math.Cos(Angle);
                                break;
                        }

                        double bar = (x - selection.Left) * Math.Sin(Angle + Skew) + (y - selection.Top) * Math.Cos(Angle + Skew);

                        if (AA)
                        {
                            double blendFactor1 = GetBlendFactor(maxDistance, distance, (int)Math.Floor(bar));
                            double blendFactor2 = GetBlendFactor(maxDistance, distance, (int)Math.Ceiling(bar));
                            double aaFactor = bar - Math.Floor(bar);
                            blendFactor = blendFactor1 * (1 - aaFactor) + blendFactor2 * aaFactor;
                        }
                        else
                        {
                            blendFactor = GetBlendFactor(maxDistance, distance, (int)(bar + 0.5));
                        }

                        if (this.GammaAdjust)
                            *ptr = ColorBgraBlender.Blend(Color1, Color2, blendFactor);
                        else
                            *ptr = ColorBgra.Lerp(Color1, Color2, blendFactor);

                        ++ptr;
                    }
                }
            }
        }

        private double GetBlendFactor(double maxDistance, double distance, int barIndex)
        {
            while (barIndex < 0)
            {
                barIndex += LengthTable.Length / 2 - (LengthTable.Length / 2) % (Width + Spacing);
            }

            double factor = 0;
            double barLength = maxDistance * LengthTable[barIndex];

            double adjustedDistance = distance - Alignment * (maxDistance - barLength);

            if (barLength > 0)
            {
                switch (Repetition)
                {
                    case RepeatMode.None:
                        factor = (adjustedDistance / barLength).Clamp(0, 1);
                        break;

                    case RepeatMode.Repeat:
                        adjustedDistance %= barLength;
                        if (adjustedDistance < 0)
                            adjustedDistance += barLength;
                        factor = adjustedDistance / barLength;
                        if (AA)
                        {
                            double aaFactor = barLength - adjustedDistance;
                            if (aaFactor > 0 && aaFactor < 1)
                            {
                                factor = aaFactor;
                            }
                        }
                        break;

                    case RepeatMode.Mirror:
                        factor = Math.Abs(adjustedDistance / barLength) % 2;
                        if (factor > 1)
                            factor = 2 - factor;
                        break;
                }
            }
            else
            {
                if (AA)
                    factor = (adjustedDistance / (barLength + 1)).Clamp(0, 1);
                else
                    factor = (adjustedDistance / barLength).Clamp(0, 1);
            }
            return factor;
        }
    }
}