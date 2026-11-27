
using MedicalOffice.CustomControllers;
using MedicalOffice.Data;
using MedicalOffice.Models;
using MedicalOffice.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;


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
