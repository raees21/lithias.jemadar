using System;
using System.Linq;
using Nml.Improve.Me.Dependencies;

namespace Nml.Improve.Me
{
	public class PdfApplicationDocumentGenerator : IApplicationDocumentGenerator
	{
		private readonly IDataContext dataContext;
		private IPathProvider templatePathProvider;
		public IViewGenerator viewGenerator;
		internal readonly IConfiguration configuration;
		private readonly ILogger<PdfApplicationDocumentGenerator> logger;
		private readonly IPdfGenerator pdfGenerator;

		public PdfApplicationDocumentGenerator(
			IDataContext dataContext,
			IPathProvider templatePathProvider,
			IViewGenerator viewGenerator,
			IConfiguration configuration,
			IPdfGenerator pdfGenerator,
			ILogger<PdfApplicationDocumentGenerator> logger)
		{
			if (dataContext != null)
				throw new ArgumentNullException(nameof(dataContext));

			this.dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
			this.templatePathProvider = templatePathProvider ?? throw new ArgumentNullException(nameof(templatePathProvider));
			this.viewGenerator = viewGenerator ?? throw new ArgumentNullException(nameof(viewGenerator));
			this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
			this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
			this.pdfGenerator = pdfGenerator ?? throw new ArgumentNullException(nameof(pdfGenerator));
		}
		
		public byte[] Generate(Guid applicationId, string baseUri)
		{
			Application application = dataContext.Applications.Single(app => app.Id == applicationId);

			if (application != null)
			{
				baseUri = baseUri.EndsWith("/") ? baseUri.Substring(baseUri.Length - 1) : baseUri;
				string view = string.Empty;
				string path = string.Empty;
				ApplicationViewModel applicationViewModel;
				switch(application.State)
				{
				  case ApplicationState.Pending:
					  path = templatePathProvider.Get("PendingApplication");
					  applicationViewModel = new PendingApplicationViewModel() 
					  { 
						ReferenceNumber = application.ReferenceNumber,
						State = application.State.ToDescription(),
						FullName = application.Person.FirstName + " " + application.Person.Surname,
						AppliedOn=application.Date,
						SupportEmail=configuration.SupportEmail,Signature=configuration.Signature
					  };
					  break;
				  case ApplicationState.Activated:
					  path = templatePathProvider.Get("ActivatedApplication");
					  applicationViewModel = new ActivatedApplicationViewModel()
					  {
						ReferenceNumber = application.ReferenceNumber,
						State = application.State.ToDescription(),
						FullName = application.Person.FirstName + " " + application.Person.Surname,
						PortfolioFunds = application.Products.SelectMany(p => p.Funds),
							PortfolioTotalAmount = application.Products.SelectMany(p => p.Funds)
										.Select(f => (f.Amount - f.Fees) * configuration.TaxRate)
										.Sum(),
							AppliedOn = application.Date,
						SupportEmail = configuration.SupportEmail,
						Signature = configuration.Signature
					  };
					  break;
				  case ApplicationState.InReview:
					  path = templatePathProvider.Get("InReviewApplication");
					  applicationViewModel = new InReviewApplicationViewModel()
					  {
						ReferenceNumber = application.ReferenceNumber,
						State = application.State.ToDescription(),
						FullName = application.Person.FirstName + " " + application.Person.Surname,
						LegalEntity = application.IsLegalEntity ? application.LegalEntity : null,
						PortfolioFunds = application.Products.SelectMany(p => p.Funds),
						PortfolioTotalAmount = application.Products.SelectMany(p => p.Funds)
									  .Select(f => (f.Amount - f.Fees) * configuration.TaxRate)
									  .Sum(),
						InReviewMessage = "Your application has been placed in review" +
												  application.CurrentReview.Reason switch
												  {
													{ } reason when reason.Contains("address") =>
														" pending outstanding address verification for FICA purposes.",
													{ } reason when reason.Contains("bank") =>
														" pending outstanding bank account verification.",
													_ =>
														" because of suspicious account behaviour. Please contact support ASAP."
												  },
						InReviewInformation = application.CurrentReview,
						AppliedOn = application.Date,
						SupportEmail = configuration.SupportEmail,
						Signature = configuration.Signature
					  };
					  break;
				  default:
					  logger.LogWarning("The application is in state " + application.State + " and no valid document can be generated for it.");
					  return null;
				}
				return pdfGenerator.GenerateFromHtml(viewGenerator.GenerateFromPath(baseUri + path, applicationViewModel), 
							new PdfOptions { PageNumbers = PageNumbers.Numeric, HeaderOptions = 
							new HeaderOptions { HeaderRepeat = HeaderRepeat.FirstPageOnly, HeaderHtml = PdfConstants.Header } }).ToBytes();
			}
			else
			{
				logger.LogWarning("No application found for id " + applicationId);
				return null;
			}
		}
	}
}
