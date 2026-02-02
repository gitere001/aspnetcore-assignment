namespace Queue_Management_System.Helpers
{
	public static class ServiceIconHelper
	{
		private static readonly Dictionary<string, (string IconClass, string ColorClass)> ServiceIconMap = new()
		{
			["DOC-"] = ("fa-user-doctor", "bg-primary"),
			["LAB-"] = ("fa-vials", "bg-success"),
			["PHAR-"] = ("fa-pills", "bg-warning"),
			["REG-"] = ("fa-file-invoice", "bg-info"),
			["EMER-"] = ("fa-ambulance", "bg-danger"),
			["XRAY-"] = ("fa-x-ray", "bg-secondary"),
			["USND-"] = ("fa-heart-pulse", "bg-purple"),
			["BILL-"] = ("fa-credit-card", "bg-dark"),

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