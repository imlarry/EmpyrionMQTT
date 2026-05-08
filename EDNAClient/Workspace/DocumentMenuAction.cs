namespace EDNAClient.Workspace
{
    public class DocumentMenuAction
    {
        public string Header  { get; set; } = "";
        public Action Execute { get; set; } = () => { };
    }
}
