﻿using SIMeasurement;
using System;
using System.Diagnostics;

namespace MeasurementExtension
{
    [Viking.Common.MenuAttribute("Measurement")]
    class MeasurementMenu
    {
        [Viking.Common.MenuItem("Set Scale")]
        public static void OnMenuSetScale(object sender, EventArgs e)
        {
            Debug.Print("Set Scale");

            using (ScaleForm form = new ScaleForm(Global.UnitOfMeasure.ToString(), Global.UnitsPerPixel))
            {

                if (form.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    Global._UnitsPerPixel = form.UnitsPerPixel;
                    Global._UnitOfMeasure = (SILengthUnits)Enum.Parse(typeof(SILengthUnits), form.UnitsOfMeasure);
                }
            }
        }

        [Viking.Common.MenuItem("Show Scale Bar")]
        public static void OnMenuShowScaleBar(object sender, EventArgs e)
        {
            Debug.Print("Show Scale Bar");

            Measurement.Properties.Settings.Default.ShowScaleBar = !Measurement.Properties.Settings.Default.ShowScaleBar;
            Measurement.Properties.Settings.Default.Save();
        }

        [Viking.Common.MenuItem("Measure Line")]
        public static void OnMenuMeasureLine(object sender, EventArgs e)
        {
            Debug.Print("Measure Line");

            Viking.UI.Controls.SectionViewerControl viewer = Viking.UI.State.ViewerControl;

            viewer.CommandQueue.EnqueueCommand<MeasureCommand>(viewer, Global.PixelWidth );
        }
    }
}
