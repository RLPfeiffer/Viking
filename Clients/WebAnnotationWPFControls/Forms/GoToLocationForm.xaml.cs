﻿using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using WebAnnotationModel;

namespace WebAnnotation.UI.Forms
{
    /// <summary>
    /// Interaction logic for GoToLocationForm.xaml
    /// </summary>
    public partial class GoToLocationForm 
    {
        public long LocationID;

        /// <summary>
        /// Called when the user requests we go to an ID
        /// </summary>
        public event Action<Int64> OnGo;

        public GoToLocationForm()
        {
            InitializeComponent();
        }
         
        private void OK_Button_Click(object sender, RoutedEventArgs e)
        { 
            try
            {
                this.LocationID = System.Convert.ToInt64(this.NumberTextbox.Text);
            }
            catch(FormatException)
            {
                return;
            }

            if(Store.Locations.GetObjectByID(this.LocationID, true) != null)
            {
                OnGo?.Invoke(LocationID);

                //TODO: Set a property that fires an event so WebAnnotation can travel where it needs to go
                //WebAnnotation.AnnotationOverlay.GoToLocation(this.LocationID);
                this.Close();
            }
        }

        private void Go_Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.LocationID = System.Convert.ToInt64(this.NumberTextbox.Text);
            }
            catch (FormatException)
            {
                return;
            }

            OnGo?.Invoke(LocationID);

            //WebAnnotation.AnnotationOverlay.GoToLocation(this.LocationID);
        }

        private void Cancel_Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private static bool IsTextAllowed(string text)
        {
            Regex regex = new Regex("[^0-9]*"); //regex that matches disallowed text
            return regex.IsMatch(text);
        }

        private void NumberTextbox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsTextAllowed(e.Text);
        }
    }
}
