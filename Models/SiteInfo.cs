namespace WebLinhKienPc.Models
{
	public class SiteInfo
	{
		public int Id { get; set; }
		public string Name { get; set; }

		public string Phone { get; set; }
		public string Email { get; set; }
		public string SiteURL { get; set; }
		public string Slogan { get; set; }
		public string? LogoUrl { get; set; }

		public List<SiteAddress>? Addresses { get; set; }
		public List<WorkHour>? WorkHours { get; set; }
	}

	public class WorkHour
	{
		public int Id { get; set; }
		public DayOfWeek StartDay { get; set; }
		public DayOfWeek EndDay { get; set; }
		public TimeSpan OpenHour { get; set; }
		public TimeSpan CloseHour { get; set; }
		public int SiteInfoId { get; set; }
		public SiteInfo? SiteInfo { get; set; }
	}
	public class SiteAddress
	{
		public int Id { get; set; }

		public string? Branch { get; set; }
		public string? StreetNumber { get; set; }
		public string? Street { get; set; }
		public string? Ward { get; set; }
		public string? District { get; set; }
		public string? City { get; set; }

		public int SiteInfoId { get; set; }
		public SiteInfo? SiteInfo { get; set; }

		public string FullAddress =>
			string.Join(", ", new[]
			{
		$"{StreetNumber} {Street}".Trim(),
		Ward,
		District,
		City
			}.Where(x => !string.IsNullOrWhiteSpace(x)));
	}
}
