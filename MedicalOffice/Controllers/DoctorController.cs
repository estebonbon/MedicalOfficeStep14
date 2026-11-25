
using MedicalOffice.CustomControllers;
using MedicalOffice.Data;
using MedicalOffice.Models;
using MedicalOffice.Utilities;
using MedicalOffice.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Storage;

public class DoctorController : ElephantController
{
    private readonly MedicalOfficeContext _context;

    public DoctorController(MedicalOfficeContext context)
    {
        _context = context;
    }

    // GET: DOCTORS
    public async Task<IActionResult> Index(int? page, int? pageSizeID)
    {
        var doctors = _context.Doctors
             .Include(d => d.DoctorSpecialties)
                .ThenInclude(d => d.Specialty)
            .Include(d => d.DoctorDocuments)
             .AsNoTracking();

        //Handle Paging
        int pageSize = PageSizeHelper.SetPageSize(HttpContext, pageSizeID, ControllerName());
        ViewData["pageSizeID"] = PageSizeHelper.PageSizeList(pageSize);
        var pagedData = await PaginatedList<Doctor>.CreateAsync(doctors.AsNoTracking(), page ?? 1, pageSize);

        return View(pagedData);
    }

    // GET: DOCTORS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var doctor = await _context.Doctors
            .Include(d => d.Patients)
            .Include(d => d.DoctorSpecialties)
                .ThenInclude(d => d.Specialty)
            .Include(d => d.DoctorDocuments)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ID == id);

        if (doctor == null)
        {
            return NotFound();
        }

        return View(doctor);
    }

    // GET: DOCTORS/Create
    public async Task<IActionResult> Create()
    {
        Doctor doctor = new Doctor();
        await PopulateAssignedSpecialtyDataAsync(doctor);
        return View(doctor);
    }

    // POST: DOCTORS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("ID,FirstName,MiddleName,LastName,Patients")] Doctor doctor, string[] selectedOptions, List<IFormFile> theFiles)
    {
        try
        {
            if (ModelState.IsValid)
            {
                await AddDocumentsAsync(doctor, theFiles);
                _context.Add(doctor);
                await _context.SaveChangesAsync();
                return RedirectToAction("Details", new { doctor.ID });
            }
        }
        catch (RetryLimitExceededException /* dex */)
        {
            ModelState.AddModelError("", "Unable to save changes after multiple attempts. Try again, and if the problem persists, see your system administrator.");
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists see your system administrator.");
        }

        //Validation Error so give the user another chance.
        await PopulateAssignedSpecialtyDataAsync(doctor);
        return View(doctor);
    }

    // GET: DOCTORS/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var doctor = await _context.Doctors
               .Include(d => d.DoctorSpecialties)
                    .ThenInclude(d => d.Specialty)
                .Include(d => d.DoctorDocuments)
               .FirstOrDefaultAsync(d => d.ID == id);

        if (doctor == null)
        {
            return NotFound();
        }

        await PopulateAssignedSpecialtyDataAsync(doctor);
        return View(doctor);
    }

    // POST: DOCTORS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string[] selectedOptions, List<IFormFile> theFiles)
    {
        //Go get the Doctor to update
        var doctorToUpdate = await _context.Doctors
            .Include(d => d.DoctorSpecialties)
                .ThenInclude(d => d.Specialty)
            .Include(d => d.DoctorDocuments)
            .FirstOrDefaultAsync(p => p.ID == id);

        //Check that you got it or exit with a not found error
        if (doctorToUpdate == null)
        {
            return NotFound();
        }

        //Update the Doctor's Specialties
        await UpdateDoctorSpecialtiesAsync(selectedOptions, doctorToUpdate);

        //Try updating it with the values posted
        if (await TryUpdateModelAsync<Doctor>(doctorToUpdate, "",
                d => d.FirstName, d => d.MiddleName, d => d.LastName))
        {
            try
            {
                await AddDocumentsAsync(doctorToUpdate, theFiles);
                await _context.SaveChangesAsync();
                return RedirectToAction("Details", new { doctorToUpdate.ID });
            }
            catch (RetryLimitExceededException /* dex */)
            {
                ModelState.AddModelError("", "Unable to save changes after multiple attempts. Try again, and if the problem persists, see your system administrator.");
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DoctorExists(doctorToUpdate.ID))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists see your system administrator.");
            }
        }

        //Validation Error so give the user another chance.
        await PopulateAssignedSpecialtyDataAsync(doctorToUpdate);
        return View(doctorToUpdate);
    }

    // GET: DOCTORS/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var doctor = await _context.Doctors
            .Include(d => d.DoctorSpecialties)
                .ThenInclude(d => d.Specialty)
            .Include(d => d.DoctorDocuments)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ID == id);

        if (doctor == null)
        {
            return NotFound();
        }

        return View(doctor);
    }

    // POST: DOCTORS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var doctor = await _context.Doctors
            .Include(d => d.DoctorSpecialties)
                .ThenInclude(d => d.Specialty)
            .Include(d => d.DoctorDocuments)
            .FirstOrDefaultAsync(m => m.ID == id);

        try
        {
            if (doctor != null)
            {
                _context.Doctors.Remove(doctor);
            }

            await _context.SaveChangesAsync();

            var returnUrl = ViewData["returnURL"]?.ToString();
            if (string.IsNullOrEmpty(returnUrl))
            {
                return RedirectToAction(nameof(Index));
            }
            return Redirect(returnUrl);
        }
        catch (DbUpdateException dex)
        {
            if (dex.GetBaseException().Message.Contains("FOREIGN KEY constraint failed"))
            {
                ModelState.AddModelError("", "Unable to Delete Doctor. Remember, you cannot delete a " +
                    "Doctor that has patients assigned.");
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem" +
                    " persists see your system administrator.");
            }
        }
        return View(doctor);

    }

    public async Task<FileContentResult> Download(int id)
    {
        var theFile = await _context.UploadedFiles
            .Include(d => d.FileContent)
            .Where(f => f.ID == id)
            .FirstOrDefaultAsync();

        if (theFile?.FileContent?.Content == null || theFile.MimeType == null)
        {
            return new FileContentResult(Array.Empty<byte>(), "application/octet-stream");
        }

        return File(theFile.FileContent.Content, theFile.MimeType, theFile.FileName);
    }

    private async Task AddDocumentsAsync(Doctor doctor, List<IFormFile> theFiles)
    {
        if (doctor == null || theFiles == null)
            return;

        foreach (var f in theFiles)
        {
            if (f != null)
            {
                string mimeType = f.ContentType;
                string fileName = Path.GetFileName(f.FileName);
                long fileLength = f.Length;

                if (!string.IsNullOrWhiteSpace(fileName) && fileLength > 0)
                {
                    using var memoryStream = new MemoryStream();
                    await f.CopyToAsync(memoryStream);

                    var document = new DoctorDocument
                    {
                        MimeType = mimeType,
                        FileName = fileName,
                        FileContent = new FileContent
                        {
                            Content = memoryStream.ToArray()
                        }
                    };

                    doctor.DoctorDocuments.Add(document);
                }
            }
        }
    }

    private async Task PopulateAssignedSpecialtyDataAsync(Doctor doctor)
    {
        var allSpecialties = await _context.Specialties
            .OrderBy(s => s.SpecialtyName)
            .ToListAsync();

        var assignedSpecialtyIds = new HashSet<int>(
            doctor.DoctorSpecialties?.Select(ds => ds.SpecialtyID) ?? Enumerable.Empty<int>()
        );

        var selected = new List<ListOptionVM>();
        var available = new List<ListOptionVM>();

        foreach (var specialty in allSpecialties)
        {
            var option = new ListOptionVM
            {
                ID = specialty.ID,
                DisplayText = specialty.SpecialtyName
            };

            if (assignedSpecialtyIds.Contains(specialty.ID))
            {
                selected.Add(option);
            }
            else
            {
                available.Add(option);
            }
        }

        ViewData["selOpts"] = new MultiSelectList(selected, "ID", "DisplayText");
        ViewData["availOpts"] = new MultiSelectList(available, "ID", "DisplayText");
    }

    private async Task UpdateDoctorSpecialtiesAsync(string[] selectedOptions, Doctor doctorToUpdate)
    {
        if (selectedOptions == null || selectedOptions.Length == 0)
        {
            doctorToUpdate.DoctorSpecialties = new List<DoctorSpecialty>();
            return;
        }

        var selectedIds = selectedOptions.Select(int.Parse).ToHashSet();
        var currentIds = doctorToUpdate.DoctorSpecialties.Select(ds => ds.SpecialtyID).ToHashSet();

        var allSpecialties = await _context.Specialties.ToListAsync();

        foreach (var specialty in allSpecialties)
        {
            var specialtyId = specialty.ID;
            var isSelected = selectedIds.Contains(specialtyId);
            var isCurrentlyAssigned = currentIds.Contains(specialtyId);

            if (isSelected && !isCurrentlyAssigned)
            {
                doctorToUpdate.DoctorSpecialties.Add(new DoctorSpecialty
                {
                    SpecialtyID = specialtyId,
                    DoctorID = doctorToUpdate.ID
                });
            }

            if (!isSelected && isCurrentlyAssigned)
            {
                var toRemove = doctorToUpdate.DoctorSpecialties
                    .FirstOrDefault(ds => ds.SpecialtyID == specialtyId);

                if (toRemove != null)
                {
                    _context.DoctorSpecialties.Remove(toRemove);
                }
            }
        }
    }

    private bool DoctorExists(int? id)
    {
        return _context.Doctors.Any(e => e.ID == id);
    }
}
