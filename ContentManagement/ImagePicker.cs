using System.ComponentModel;
using System.Drawing.Design;
using System.Windows.Forms;

namespace ContentManagement
{
    internal class ImagePicker: UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.Modal;
        }

        public override object EditValue(ITypeDescriptorContext context, System.IServiceProvider provider, object value)
        {
            if (provider == null || context == null)
                return value;

            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = @"Image Files(*.PNG;*.JPG;*.GIF)|*.PNG;*.JPG;*.GIF";

                if (dialog.ShowDialog() != DialogResult.OK)
                    return value;

                value = PersistenceManager.SaveImage(context.Instance, dialog.FileName);
            }

            return value;
        }
    }
}
