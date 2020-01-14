
using System;
using System.Collections.Generic;

using UnityEngine;

using Image = UnityEngine.Texture2D;
using uFont = UnityEngine.Font;

[Serializable]
public class AppResources
{
	public static void LoadIfNull<T>(ref T field, string defaultResourceName) where T : UnityEngine.Object {
		if (field == null) { field = Resources.Load<T>(defaultResourceName); }
	}
    public List<uFont> Fonts;

    public ReservedResources Images;

    /// <summary> System resources. </summary>
    [Serializable]
    public struct ReservedResources
    {
		public void InitDefaults() {
			LoadIfNull(ref ArrowDown, "arrow_down");
			LoadIfNull(ref ArrowLeft, "arrow_left");
			LoadIfNull(ref ArrowRight, "arrow_right");
			LoadIfNull(ref ArrowUp, "arrow_up");
			LoadIfNull(ref Circle, "circle");
			LoadIfNull(ref Checked, "checked");
			LoadIfNull(ref Close, "close");
			LoadIfNull(ref CurvedArrowDown, "curved_arrow_down");
			LoadIfNull(ref CurvedArrowLeft, "curved_arrow_left");
			LoadIfNull(ref CurvedArrowRight, "curved_arrow_right");
			LoadIfNull(ref CurvedArrowUp, "curved_arrow_up");
			LoadIfNull(ref DateTimePicker, "datetimepicker");
			LoadIfNull(ref DropDownRightArrow, "dropdown_rightArrow");
			LoadIfNull(ref FileDialogBack, "filedialog_back");
			LoadIfNull(ref FileDialogFile, "filedialog_file");
			LoadIfNull(ref FileDialogFolder, "filedialog_folder");
			LoadIfNull(ref FileDialogRefresh, "filedialog_refresh");
			LoadIfNull(ref FileDialogUp, "filedialog_up");

			LoadIfNull(ref FormResize, "form_resize");
			LoadIfNull(ref NumericDown, "numeric_down");
			LoadIfNull(ref NumericUp, "numeric_up");
			LoadIfNull(ref RadioButton_Checked, "radioButton_checked");
			LoadIfNull(ref RadioButton_Hovered, "radioButton_hovered");
			LoadIfNull(ref RadioButton_Unchecked, "radioButton_unchecked");

			LoadIfNull(ref TreeNodeCollapsed, "treenode_collapsed");
			LoadIfNull(ref TreeNodeExpanded, "treenode_expanded");
			Cursors.InitDefaults();
		}
        [Tooltip("Form resize icon")]
        public Image ArrowDown;

        [Tooltip("Form resize icon, MonthCalendar, TabControl")]
        public Image ArrowLeft;

        [Tooltip("Form resize icon, MonthCalendar, TabControl")]
        public Image ArrowRight;

        [Tooltip("Form resize icon")]
        public Image ArrowUp;

        public Image Circle;

        [Tooltip("Checkbox, ToolStripItem")]
        public Image Checked;

        [Tooltip("Form close button")]
        public Image Close;

        public CursorImages Cursors;

        [Tooltip("ComboBox, VScrollBar")]
        public Image CurvedArrowDown;

        [Tooltip("HScrollBar")]
        public Image CurvedArrowLeft;

        [Tooltip("HScrollBar")]
        public Image CurvedArrowRight;

        [Tooltip("VScrollBar")]
        public Image CurvedArrowUp;

        [Tooltip("DateTimePicker button")]
        public Image DateTimePicker;

        [Tooltip("ToolStripDropDown")]
        public Image DropDownRightArrow;

        public Image FileDialogBack;
        public Image FileDialogFile;
        public Image FileDialogFolder;
        public Image FileDialogRefresh;
        public Image FileDialogUp;
        
        public Image FormResize;

        [Tooltip("NumericUpDown")]
        public Image NumericDown;

        [Tooltip("NumericUpDown")]
        public Image NumericUp;

        public Image RadioButton_Checked;
        public Image RadioButton_Hovered;
        public Image RadioButton_Unchecked;

        [Tooltip("Tree")]
        public Image TreeNodeCollapsed;

        [Tooltip("Tree")]
        public Image TreeNodeExpanded;
    }

    [Serializable]
    public struct CursorImages
    {
        [Tooltip("Leave this field empty if you don't want to use your own cursor.")]
        public Image Default;

        public Image Hand;
        public Image Help;
        public Image HSplit;
        public Image IBeam;
        public Image SizeAll;
        public Image SizeNESW;
        public Image SizeNS;
        public Image SizeNWSE;
        public Image SizeWE;
        public Image VSplit;

		public void InitDefaults() {
			// LoadIfNull(ref Default, "")
			LoadIfNull(ref Hand, "cursors/hand");
			LoadIfNull(ref Help, "cursors/help");
			LoadIfNull(ref HSplit, "cursors/hsplit");
			LoadIfNull(ref IBeam, "cursors/ibeam");
			LoadIfNull(ref SizeAll, "cursors/sizeall");
			LoadIfNull(ref SizeNESW, "cursors/sizenesw");
			LoadIfNull(ref SizeNS, "cursors/sizens");
			LoadIfNull(ref SizeNWSE, "cursors/sizenwse");
			LoadIfNull(ref SizeWE, "cursors/sizewe");
			LoadIfNull(ref VSplit, "cursors/vsplit");
		}
    }
}
