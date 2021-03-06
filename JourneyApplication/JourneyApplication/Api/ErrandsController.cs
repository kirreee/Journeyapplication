﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Description;
using JourneyApplication.Models;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using iTextSharp.text.pdf;
using System.Web;
using iTextSharp.text;
using System.IO;
using log4net;

namespace JourneyApplication.Api
{
    public class ErrandsController : ApiController
    {
        private static readonly ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private ApplicationDbContext db = new ApplicationDbContext();
        protected UserManager<ApplicationUser> UserManager { get; set; }

        public ErrandsController()
        {
            UserManager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(this.db));
        }

        // GET: api/Errands
        public IHttpActionResult GetErrands()
        {

            try
            {
                var userId = User.Identity.GetUserId();
                var errands = db.Errands
                    .Include(x => x.Vehicle)
                    .Where(x => x.Vehicle.User.Id == userId)
                    .ToList();
                return Ok(errands);
            }
            catch (Exception ex)
            {
                Log.Error(ex + "Error Message from log4net");
                throw ex;

            }
        }

        //GET: api/Errands-ongoing
        [Route("api/Errands-ongoing")]
        [HttpGet]
        public IHttpActionResult GetOngoingErrands()
        {
            try
            {
                var userId = User.Identity.GetUserId();
                var errands = db.Errands
                    .Include(x => x.Vehicle)
                    .Where(x => x.User.Id == userId
                    && x.Done == false)
                    .ToList();
                return Ok(errands);
            }
            catch (Exception ex)
            {
                Log.Error(ex + "Error Message from log4net");
                throw ex;

            }
        }


        [HttpPost]
        [Route("api/pdf/generate")]
        public IHttpActionResult GeneratePdf(GeneratePdf vm)
        {
            if (vm.VehicleId < 1)
            {
                return null;
            }

            var errands = db.Errands
                .Where(x => x.Vehicle.Id == vm.VehicleId
                && x.Added < vm.ToDate
                && x.Added > vm.FromDate)
                .ToList();


            PdfPTable table = new PdfPTable(7);
            table.WidthPercentage = 100;
            table.AddCell("StartAdress");
            table.AddCell("Destination");
            table.AddCell("Ankomst");
            table.AddCell("Anteckningar");
            table.AddCell("StartKm");
            table.AddCell("ArrivalKm");
            table.AddCell("Added");

            foreach (var errand in errands)
            {
                table.AddCell(errand.StartAdress);
                table.AddCell(errand.Destination);
                table.AddCell(errand.Destination);
                table.AddCell(errand.Notes);
                table.AddCell(errand.StartKm.ToString());
                table.AddCell(errand.ArrivalKm.ToString());
                table.AddCell(errand.Added.ToLongDateString());
            }

            var guidUrl = "demo-" + Guid.NewGuid().ToString() + ".pdf";
            var savePath = HttpContext.Current.Request.PhysicalApplicationPath + "/PDF/" + guidUrl;
            using (Document doc = new Document(PageSize.A4))
            {
                using (PdfWriter writer = PdfWriter.GetInstance(doc, new FileStream(savePath, FileMode.Create)))
                {
                    doc.Open();
                    doc.Add(table);
                    doc.Close();
                }
            }


            return Ok("/PDF/" + guidUrl);

        }

        // GET: api/Errands/5
        [ResponseType(typeof(Errand))]
        public IHttpActionResult GetErrand(int id)
        {
            try
            {
                Errand errand = db.Errands.Find(id);
                if (errand == null)
                {
                    return NotFound();
                }

                return Ok(errand);
            }
            catch (Exception ex)
            {
                Log.Error(ex + "Error Message from log4net");
                throw ex;

            }
        }

        // PUT: api/Errands/5
        [ResponseType(typeof(void))]
        public IHttpActionResult PutErrand(int id, Errand errand)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (id != errand.Id)
            {
                return BadRequest();
            }
            var vId = db.Vehicles.Find(errand.VehicleId);
            errand.Vehicle = vId;
            errand.Done = true;
            errand.ArrivalKm = errand.ArrivalKm;
            db.Entry(errand).State = EntityState.Modified;

            try
            {
                db.SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ErrandExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }


            return StatusCode(HttpStatusCode.NoContent);
        }

        // POST: api/Errands
        [ResponseType(typeof(Errand))]
        public IHttpActionResult PostErrand(Errand errand)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            ApplicationUser user = UserManager.FindById(User.Identity.GetUserId());
            errand.User = user;
            if (errand.User == null)
            {
                return Unauthorized();
            }

            errand.Added = DateTime.Now;
            errand.Done = false;

            errand.DriveDate = errand.DriveDate.AddDays(1);
            if (errand.DriveDate < DateTime.Now)
            {
                return Ok("Error");
            }

            var vehicle = db.Vehicles.Find(errand.VehicleId);
            errand.Vehicle = vehicle;
            if (vehicle == null)
            {
                return BadRequest("Fordonet finns ej!");
            }


            if (db.Errands.Any(x => x.Vehicle.Id == vehicle.Id))
            {

                try
                {
                    //Get lastDriveDate
                    var lastErrandDriveDate = db.Errands
                        .Where(x => x.Vehicle.Id == vehicle.Id)
                        .Max(x => x.DriveDate);

                    //Get Last ErrandDriveDate
                    var lastErrand = db.Errands
                        .First(x => x.DriveDate == lastErrandDriveDate);

                    if (errand.StartKm < lastErrand.ArrivalKm)
                    {
                        return Ok("Warning");
                    }
                    db.Errands.Add(errand);
                    db.Entry(errand.Vehicle).State = EntityState.Unchanged;
                    db.SaveChanges();
                }
                catch (Exception ex)
                {
                    Log.Error(ex + "Error Message from log4net");

                }
            }
            else
            {
                db.Errands.Add(errand);
                db.Entry(errand.Vehicle).State = EntityState.Unchanged;
                db.SaveChanges();
                return Ok("Warn");
            }

            return CreatedAtRoute("DefaultApi", new { id = errand.Id }, errand);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        private bool ErrandExists(int id)
        {
            return db.Errands.Count(e => e.Id == id) > 0;
        }
    }
}