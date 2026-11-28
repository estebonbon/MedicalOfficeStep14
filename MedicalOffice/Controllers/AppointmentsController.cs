
using MedicalOffice.CustomControllers;
using MedicalOffice.Data;
using MedicalOffice.Models;
using MedicalOffice.Utilities;
using MedicalOffice.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;


public class AppointmentsController : ElephantController
{
    private readonly MedicalOfficeContext _context;

    public AppointmentsController(MedicalOfficeContext context)
    {
        _context = context;
    }

    // GET: Appointment
    public async Task<IActionResult> Index(DateTime StartDate, DateTime EndDate, string? SearchString, int? DoctorID, int? PatientID, int? AppointmentReasonID, int? page, int? pageSizeID, string? actionButton, string sortDirection = "asc", string sortField = "Appointment")
    {
        //List of sort options
        string[] sortOptions = new[]
        {
        "Appointment", "Appt. Reason", "Extra Fees", "Doctor", "Patient"
    };

        //Count the number of filters applied
        ViewData["Filtering"] = "btn-outline-secondary";
        int numberFilters = 0;

        //Data for SelectLists
        ViewData["DoctorID"] = DoctorSelectList();
        ViewData["PatientID"] = PatientSelectList();
        ViewData["AppointmentReasonID"] = AppointmentReasonList(true);

        //Always filter by date range
        if (EndDate == DateTime.MinValue)
        {
            if (_context.Appointments.Any())
            {
                StartDate = _context.Appointments.Min(o => o.StartTime).Date;
                EndDate = _context.Appointments.Max(o => o.StartTime).Date;
            }
            else
            {
                StartDate = DateTime.Today;
                EndDate = DateTime.Today;
            }
        }

        //Check the order of the dates and swap them if required
        if (EndDate < StartDate)
        {
            DateTime temp = EndDate;
            EndDate = StartDate;
            StartDate = temp;
        }

        //Save date range to ViewData
        ViewData["StartDate"] = StartDate.ToString("yyyy-MM-dd");
        ViewData["EndDate"] = EndDate.ToString("yyyy-MM-dd");

        //Start with Includes and IQueryable
        var appointments = _context.Appointments
            .Include(a => a.AppointmentReason)
            .Include(a => a.Doctor)
            .Include(a => a.Patient)
            .Where(a => a.StartTime >= StartDate && a.StartTime < EndDate.AddDays(1))
            .AsNoTracking();

        //Filter by Doctor
        if (DoctorID.HasValue)
        {
            appointments = appointments.Where(p => p.DoctorID == DoctorID);
            numberFilters++;
        }

        //Filter by Patient
        if (PatientID.HasValue)
        {
            appointments = appointments.Where(p => p.PatientID == PatientID);
            numberFilters++;
        }

        //Filter by Appointment Reason
        //Different because it is nullable
        if (AppointmentReasonID.HasValue)
        {
            if (AppointmentReasonID == 0)
            {
                appointments = appointments.Where(p => p.AppointmentReasonID == null);
            }
            else
            {
                appointments = appointments.Where(p => p.AppointmentReasonID == AppointmentReasonID);
            }

            numberFilters++;
        }

        //Search Notes
        if (!String.IsNullOrEmpty(SearchString))
        {
            appointments = appointments.Where(p =>
                p.Notes != null && p.Notes.Contains(SearchString));

            numberFilters++;
        }

        //Give feedback about the filters applied
        if (numberFilters > 0)
        {
            ViewData["Filtering"] = "btn-danger";
            ViewData["numberFilters"] = "(" + numberFilters + " Filters Applied)";
            ViewData["ShowFilter"] = "show";
        }

        //Handle sorting
        if (!String.IsNullOrEmpty(actionButton))
        {
            page = 1;

            if (sortOptions.Contains(actionButton))
            {
                if (actionButton == sortField)
                {
                    sortDirection = sortDirection == "asc" ? "desc" : "asc";
                }
                else
                {
                    sortDirection = "asc";
                }

                sortField = actionButton;
            }
        }

        //Apply sorting
        if (sortField == "Doctor")
        {
            if (sortDirection == "asc")
            {
                appointments = appointments
                    .OrderBy(p => p.Doctor!.LastName)
                    .ThenBy(p => p.Doctor!.FirstName);
            }
            else
            {
                appointments = appointments
                    .OrderByDescending(p => p.Doctor!.LastName)
                    .ThenByDescending(p => p.Doctor!.FirstName);
            }
        }
        else if (sortField == "Patient")
        {
            if (sortDirection == "asc")
            {
                appointments = appointments
                    .OrderBy(p => p.Patient!.LastName)
                    .ThenBy(p => p.Patient!.FirstName);
            }
            else
            {
                appointments = appointments
                    .OrderByDescending(p => p.Patient!.LastName)
                    .ThenByDescending(p => p.Patient!.FirstName);
            }
        }
        else if (sortField == "Appt. Reason")
        {
            if (sortDirection == "asc")
            {
                appointments = appointments
                    .OrderBy(p => p.AppointmentReason!.ReasonName);
            }
            else
            {
                appointments = appointments
                    .OrderByDescending(p => p.AppointmentReason!.ReasonName);
            }
        }
        else if (sortField == "Extra Fees")
        {
            if (sortDirection == "asc")
            {
                appointments = appointments.OrderBy(p => p.ExtraFee);
            }
            else
            {
                appointments = appointments.OrderByDescending(p => p.ExtraFee);
            }
        }
        else
        {
            if (sortDirection == "asc")
            {
                appointments = appointments.OrderBy(p => p.StartTime);
            }
            else
            {
                appointments = appointments.OrderByDescending(p => p.StartTime);
            }
        }

        //Save sorting information
        ViewData["sortField"] = sortField;
        ViewData["sortDirection"] = sortDirection;

        //Handle Paging
        int pageSize = PageSizeHelper.SetPageSize(HttpContext, pageSizeID, ControllerName());
        ViewData["pageSizeID"] = PageSizeHelper.PageSizeList(pageSize);

        var pagedData = await PaginatedList<Appointment>
            .CreateAsync(appointments, page ?? 1, pageSize);

        return View(pagedData);
    }
    // GET: APPOINTMENTS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var appointment = await _context.Appointments
            .FirstOrDefaultAsync(m => m.ID == id);
        if (appointment == null)
        {
            return NotFound();
        }

        return View(appointment);
    }

    public IActionResult DownloadAppointments()
    {
        //Get the appointments
        var appts = from a in _context.Appointments
                    .Include(a => a.AppointmentReason)
                    .Include(a => a.Patient)
                    .ThenInclude(p => p.Doctor)
                    orderby a.StartTime descending
                    select new //Anonymous type
                    {
                        Date = a.StartTime.ToShortDateString(),
                        Patient = a.Patient.Summary,
                        Reason = a.AppointmentReason.ReasonName,
                        Fee = a.ExtraFee,
                        Phone = a.Patient.PhoneFormatted,
                        Doctor = a.Patient.Doctor.Summary,
                        a.Notes
                    };
        //How many rows?
        int numRows = appts.Count();

        if (numRows > 0) //We have data
        {
            //Create a new spreadsheet from scratch.
            using (ExcelPackage excel = new ExcelPackage())
            {

                //Note: you can also pull a spreadsheet out of the database if you
                //have saved it in the normal way we do, as a Byte Array in a Model
                //such as the UploadedFile class.
                //
                // Suppose...
                //
                // var theSpreadsheet = _context.UploadedFiles.Include(f => f.FileContent).Where(f => f.ID == id).SingleOrDefault();
                //
                //    //Pass the Byte[] FileContent to a MemoryStream
                //
                // using (MemoryStream memStream = new MemoryStream(theSpreadsheet.FileContent.Content))
                // {
                //     ExcelPackage package = new ExcelPackage(memStream);
                // }

                var workSheet = excel.Workbook.Worksheets.Add("Appointments");

                //Note: Cells[row, column]
                workSheet.Cells[3, 1].LoadFromCollection(appts, true);

                //Style first column for dates
                workSheet.Column(1).Style.Numberformat.Format = "yyyy-mm-dd";

                //Style fee column for currency
                workSheet.Column(4).Style.Numberformat.Format = "###,##0.00";

                //Note: You can define a BLOCK of cells: Cells[startRow, startColumn, endRow, endColumn]
                //Make Date and Patient Bold
                workSheet.Cells[4, 1, numRows + 3, 2].Style.Font.Bold = true;

                //Note: these are fine if you are only 'doing' one thing to the range of cells.
                //Otherwise you should USE a range object for efficiency
                using (ExcelRange totalfees = workSheet.Cells[numRows + 4, 4])//
                {
                    totalfees.Formula = "Sum(" + workSheet.Cells[4, 4].Address + ":" + workSheet.Cells[numRows + 3, 4].Address + ")";
                    totalfees.Style.Font.Bold = true;
                    totalfees.Style.Numberformat.Format = "$###,##0.00";
                }

                //Set Style and backgound colour of headings
                using (ExcelRange headings = workSheet.Cells[3, 1, 3, 7])
                {
                    headings.Style.Font.Bold = true;
                    var fill = headings.Style.Fill;
                    fill.PatternType = ExcelFillStyle.Solid;
                    fill.BackgroundColor.SetColor(Color.LightBlue);
                }

                ////Boy those notes are BIG!
                ////Lets put them in comments instead.
                //for (int i = 4; i < numRows + 4; i++)
                //{
                //    using (ExcelRange Rng = workSheet.Cells[i, 7])
                //    {
                //        string[] commentWords = Rng.Value.ToString().Split(' ');
                //        Rng.Value = commentWords[0] + "...";
                //        //This LINQ adds a newline every 7 words
                //        string comment = string.Join(Environment.NewLine, commentWords
                //            .Select((word, index) => new { word, index })
                //            .GroupBy(x => x.index / 7)
                //            .Select(grp => string.Join(" ", grp.Select(x => x.word))));
                //        ExcelComment cmd = Rng.AddComment(comment, "Apt. Notes");
                //        cmd.AutoFit = true;
                //    }
                //}

                //Autofit columns
                workSheet.Cells.AutoFitColumns();
                //Note: You can manually set width of columns as well
                //workSheet.Column(7).Width = 12;

                //Add a title and timestamp at the top of the report
                workSheet.Cells[1, 1].Value = "Appointment Report";
                using (ExcelRange Rng = workSheet.Cells[1, 1, 1, 7])
                {
                    Rng.Merge = true; //Merge columns start and end range
                    Rng.Style.Font.Bold = true; //Font should be bold
                    Rng.Style.Font.Size = 18;
                    Rng.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }
                //Since the time zone where the server is running can be different, adjust to 
                //Local for us.
                DateTime utcDate = DateTime.UtcNow;
                TimeZoneInfo esTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                DateTime localDate = TimeZoneInfo.ConvertTimeFromUtc(utcDate, esTimeZone);
                using (ExcelRange Rng = workSheet.Cells[2, 7])
                {
                    Rng.Value = "Created: " + localDate.ToShortTimeString() + " on " +
                        localDate.ToShortDateString();
                    Rng.Style.Font.Bold = true; //Font should be bold
                    Rng.Style.Font.Size = 12;
                    Rng.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                }

                //Ok, time to download the Excel

                try
                {
                    Byte[] theData = excel.GetAsByteArray();
                    string filename = "Appointments.xlsx";
                    string mimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    return File(theData, mimeType, filename);
                }
                catch (Exception)
                {
                    return BadRequest("Could not build and download the file.");
                }
            }
        }
        return NotFound("No data.");
    }
    public IActionResult AappointmentSummaryExport()
    {
        //Get the Data
        var sumQ = GetAppointmentSummaries();

        //How many rows?
        int numRows = sumQ.Count();

        if (numRows > 0) //We have data
        {
            //Create a new spreadsheet from scratch.
            using (ExcelPackage excel = new ExcelPackage())
            {
                var workSheet = excel.Workbook.Worksheets.Add("Appointment Summary");

                //Note: Cells[row, column]
                workSheet.Cells[1, 1].LoadFromCollection(sumQ, true);

                //Autofit columns
                workSheet.Cells.AutoFitColumns();

                //Ok, time to download the Excel

                try
                {
                    Byte[] theData = excel.GetAsByteArray();
                    string filename = "AppointmentSummary.xlsx";
                    string mimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    return File(theData, mimeType, filename);
                }
                catch (Exception)
                {
                    return BadRequest("Could not build and download the file.");
                }
            }
        }
        return NotFound("No data.");
    }

    public async Task<IActionResult> AppointmentReasonSummary()
    {
        var sumQ = await _context.Appointments.Include(a => a.AppointmentReason)
            .GroupBy(a => new
            {
                a.AppointmentReasonID, //This doesnt mean appointmentIDS need to match, it means ReasonIDS should match, and there reason name, so there is no id for null this works around that
                ReasonName = a.AppointmentReason == null
                    ? "No Reason Given"
                    : a.AppointmentReason.ReasonName
            })
            .Select(grp => new AppointmentReasonSummaryVM
            {
                ID = grp.Key.AppointmentReasonID,
                Appointment_Reason = grp.Key.ReasonName,
                Number_Of_Appointments = grp.Count(),
                Total_Extra_Fees = grp.Sum(a => a.ExtraFee),
                Maximum_Fee_Charged = grp.Max(a => a.ExtraFee)
            })
            .OrderBy(s => s.Appointment_Reason)
            .AsNoTracking()
            .ToListAsync();

        return View(sumQ);
    }
    public async Task<IActionResult> AppointmentSummary(int? page, int? pageSizeID)
    {
        var sumQ = GetAppointmentSummaries();

        int pageSize = PageSizeHelper.SetPageSize(HttpContext, pageSizeID, "AppointmentSummary");//Remember for this View
        ViewData["pageSizeID"] = PageSizeHelper.PageSizeList(pageSize);
        var pagedData = await PaginatedList<AppointmentSummaryVM>.CreateAsync(sumQ.AsNoTracking(), page ?? 1, pageSize);

        return View(pagedData);
    }

    private IQueryable<AppointmentSummaryVM> GetAppointmentSummaries()
    {
        //return _context.Appointments.Include(a => a.Patient)
        //    .GroupBy(a => new { a.PatientID, a.Patient!.LastName, a.Patient!.FirstName, a.Patient!.MiddleName })
        //    .Select(grp => new AppointmentSummaryVM
        //    {
        //        ID = grp.Key.PatientID,
        //        First_Name = grp.Key.FirstName,
        //        Middle_Name = grp.Key.MiddleName,
        //        Last_Name = grp.Key.LastName,
        //        Number_Of_Appointments = grp.Count(),
        //        Total_Extra_Fees = grp.Sum(a => a.ExtraFee),
        //        Maximum_Fee_Charged = grp.Max(a => a.ExtraFee)
        //    })
        //    .OrderBy(s => s.Last_Name).ThenBy(s => s.First_Name);

        return _context.AppointmentSummaries.AsNoTracking();
    }


    private SelectList AppointmentReasonList(bool AddOption = false)
    {
        var AppointmentReasons = new SelectList(_context
            .AppointmentReasons
            .OrderBy(m => m.ReasonName), "ID", "ReasonName").ToList();
        if (AddOption)
        {
            AppointmentReasons.Insert(0, new SelectListItem { Value = "0", Text = "No Reason Given" });
        }
        return new SelectList(AppointmentReasons, "Value", "Text");
    }
    private SelectList DoctorSelectList()
    {
        return new SelectList(_context.Doctors
            .OrderBy(d => d.LastName)
            .ThenBy(d => d.FirstName), "ID", "Summary");
    }

    private SelectList PatientSelectList()
    {
        return new SelectList(_context.Patients
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName), "ID", "Summary");
    }

    private bool AppointmentExists(int? id)
    {
        return _context.Appointments.Any(e => e.ID == id);
    }
}
