namespace Loupedeck.ControlMapperPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using RestSharp;

    class ControlMapperRoles : PluginDynamicCommand
    {
        private List<String> _rolesNames;
        private readonly List<String> _transformations = new List<String> { "None", "Short press", "Long press", "Momentary to latching", "Hold to press", "Hold to latch" };
        private readonly String _ip = "127.0.0.1";
        private readonly SemaphoreSlim[] _semaphore = new SemaphoreSlim[128];

        public ControlMapperRoles() : base()
        {
            this._rolesNames = this.GetControlMapperRoles();
            this.DisplayName = "Trigger control mapper role";
            this.GroupName = "Roles";
            if (this._rolesNames != null)
            {
                //this.MakeProfileAction("tree");
                this.MakeProfileAction("list;Select Role: ");
                for (var i = 0; i < 128; i++)
                {
                    this._semaphore[i] = new SemaphoreSlim(1, 1);
                }
            }
        }

        protected override PluginActionParameter[] GetParameters()
        {
            this._rolesNames = this.GetControlMapperRoles();
            if (this._rolesNames == null)
            {
                return null;
            }
            else
            {
                var i = 0;
                return this._rolesNames.Select(t => new PluginActionParameter($"{t}|{i++}", t, String.Empty)).ToArray();
            }
        }

        private List<String> GetControlMapperRoles()
        {
            var client = new RestClient($"http://{this._ip}:8888");
            var request = new RestRequest("/api/ControlMapper/GetRoles", Method.Get);
            var queryResult = client.Execute<List<String>>(request).Data;
            return queryResult;
        }

        private void StartControlMapperRole(String role)
        {
            var client = new RestClient($"http://{this._ip}:8888");
            var request = new RestRequest("/api/ControlMapper/StartRole", Method.Post);
            request.AddParameter("roleName", role);
            client.Execute<List<String>>(request);
        }

        private void StopControlMapperRole(String role)
        {
            var client = new RestClient($"http://{this._ip}:8888");
            var request = new RestRequest("/api/ControlMapper/StopRole", Method.Post);
            request.AddParameter("roleName", role);
            client.Execute<List<String>>(request);
        }

        protected override PluginProfileActionData GetProfileActionData()
        {
            // To be implemented in the future (with transformation feature)
            // create tree data
            var tree = new PluginProfileActionTree("Select Role");

            // describe levels
            tree.AddLevel("Transformation");
            tree.AddLevel("Role");

            // add data tree
            foreach (var transformationName in this._transformations)
            {
                var node = tree.Root.AddNode(transformationName);
                var i = 0;
                foreach (var roleName in this._rolesNames)
                {
                    node.AddItem($"{roleName}|{transformationName}|{i}", roleName, null);
                    i += 1;
                }
            }

            // return tree data
            return tree;
        }

        protected override void RunCommand(String actionParameter)
        {
            if (!String.IsNullOrEmpty(actionParameter) && actionParameter.Contains("|"))
            {
                var role = actionParameter.Split("|")[0];
                //var trans = actionParameter.Split("|")[1];
                if (Int32.TryParse(actionParameter.Split("|")[1], out var roleId))
                {
                    var t = Task.Run(async delegate
                    {
                        if(this._semaphore[roleId].Wait(50))
                        {
                            try
                            {
                                this.StartControlMapperRole(role);
                                await Task.Delay(25);
                                this.StopControlMapperRole(role);
                                await Task.Delay(25);
                            }
                            finally
                            {
                                this._semaphore[roleId].Release();
                            }
                        }
                    });
                }

                this.ActionImageChanged(role);
            }
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            if (!String.IsNullOrEmpty(actionParameter) && actionParameter.Contains("|"))
            {
                var role = actionParameter.Split("|")[0];
                //var trans = actionParameter.Split("|")[1];
                return role;
            }
            return "Unknown role";
        }
    }
}
