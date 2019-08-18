namespace ContentManagement
{
    internal interface IImageOwner: IUnique
    {
        string Image { get; set; }
    }
}
