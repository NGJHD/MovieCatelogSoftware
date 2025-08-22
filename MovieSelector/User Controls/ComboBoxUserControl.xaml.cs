using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ComboBoxUserControl
{    
    public partial class CustomComboBox : UserControl
    {
/*************************************************************************************************************/
        public enum Position
        {
            NONE,            
            TOP,
            CENTER,
            BOTTOM,
        }
        
        private Position _listBoxPosition;
        public Position ListBoxPosition
        {
            get
            {
                return _listBoxPosition;
            }
            set
            {
                _listBoxPosition = value;

                /*if (_listBoxPosition == Position.TOP)
                {
                    listBoxPopUp.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
                    listBoxPopUp.VerticalOffset = this.Height;
                }
                else if(_listBoxPosition == Position.BOTTOM)
                {
                    listBoxPopUp.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                    listBoxPopUp.VerticalOffset = -this.Height;
                }
                else
                {
                    listBoxPopUp.Placement = System.Windows.Controls.Primitives.PlacementMode.Center;
                    listBoxPopUp.VerticalOffset = 0;
                }*/
            }
        }

        public int SelectedIndex
        {
            get
            {
                return listBox.SelectedIndex;
            }

            set
            {
                listBox.SelectedIndex = value;                
            }
        }

        public object SelectedItem
        {
            get
            {
                return listBox.SelectedItem;
            }

            set
            {
                listBox.SelectedItem = value;
            }
        }
/*************************************************************************************************************/
        public CustomComboBox()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (ListBoxPosition == Position.NONE)
                ListBoxPosition = Position.CENTER;

            Window window = GetVisualAncestor<Window>(this);

            if (window != null)
            {
                window.PreviewMouseDown += (a, b) => CloseOnParentInteraction(b.Source as FrameworkElement);
                window.PreviewTouchDown += (a, b) => CloseOnParentInteraction(b.Source as FrameworkElement);
            }
        }

        public static T GetVisualAncestor<T>(DependencyObject descendent) where T : class
        {
            T ancestor = null;
            DependencyObject scan = descendent;
            ancestor = null;

            while (scan != null && ((ancestor = scan as T) == null))
            {
                scan = VisualTreeHelper.GetParent(scan);
            }

            return ancestor;
        }

        private void CloseOnParentInteraction(FrameworkElement parent)
        {
            if (listBoxPopUp.IsOpen == false || 
               (parent.GetType() == this.GetType() && parent.Name == this.Name))
            {
                return;                
            }

            listBoxPopUp.IsOpen = false;
        }
/*************************************************************************************************************/
        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (listBoxPopUp.IsOpen == true)
            {
                listBoxPopUp.IsOpen = false;
            }
            else
            {
                listBoxPopUp.IsOpen = true;
                listBox.ScrollIntoView(listBox.Items[0]);
                /*if(listBox.SelectedItem != null)
                    listBox.ScrollIntoView(listBox.SelectedItem);*/
            }
        }

        public void AddItem(object item)
        {
            listBox.Items.Add(item.ToString());
        }

        private void listBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            listBoxPopUp.IsOpen = false;
                        
            OnSelectionChanged(e);       
        }

        private void ListBox_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            e.Handled = true;
        }
/*************************************************************************************************************/
        public event EventHandler<SelectionChangedEventArgs> SelectionChanged;

        protected virtual void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            EventHandler<SelectionChangedEventArgs> handler = SelectionChanged;
            
            if (handler != null)
            {
                handler(this, e);
            }
        }
/*************************************************************************************************************/
    }
}
