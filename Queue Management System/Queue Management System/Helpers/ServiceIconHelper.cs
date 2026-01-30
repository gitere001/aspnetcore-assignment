namespace Queue_Management_System.Helpers
{
	public static class ServiceIconHelper
	{
		private static readonly Dictionary<string, (string IconClass, string ColorClass)> ServiceIconMap = new()
		{
			["D"] = ("fa-user-doctor", "bg-primary"),
			["L"] = ("fa-vials", "bg-success"),
			["P"] = ("fa-pills", "bg-warning"),
			["R"] = ("fa-file-invoice", "bg-info"),
			["E"] = ("fa-ambulance", "bg-danger"),
			["X"] = ("fa-x-ray", "bg-secondary"),
			["U"] = ("fa-heart-pulse", "bg-purple"),
			["B"] = ("fa-credit-card", "bg-dark"),
			
		};

		public static string GetIconClass(string prefixCode)
		{
			return ServiceIconMap.ContainsKey(prefixCode)
				? ServiceIconMap[prefixCode].IconClass
				: "fa-question";
		}

		public static string GetColorClass(string prefixCode)
		{
			return ServiceIconMap.ContainsKey(prefixCode)
				? ServiceIconMap[prefixCode].ColorClass
				: "bg-secondary";
		}
	}
}