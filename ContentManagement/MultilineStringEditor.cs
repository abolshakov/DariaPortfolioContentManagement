using System;
using System.ComponentModel;
using System.Drawing.Design;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace ContentManagement
{
    internal class MultilineStringEditor: UITypeEditor
    {
        private const string VisibleLineBreak = " \n";

        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.Modal;
        }

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            var editorService = (IWindowsFormsEditorService)provider.GetService(typeof(IWindowsFormsEditorService));
            var text = value as string;
            var textEditorBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Height = 150,
                BorderStyle = BorderStyle.None,
                AcceptsReturn = true,
                Text = text?.Replace(VisibleLineBreak, Environment.NewLine)

            };
            editorService.DropDownControl(textEditorBox);

            return textEditorBox.Text?.Replace(Environment.NewLine, VisibleLineBreak);
        }
    }
}
