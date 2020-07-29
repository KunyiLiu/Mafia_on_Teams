using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MafiaCore;

namespace MafiaGame.Controllers
{
    public class MafiaController : Controller
    {
        // GET: Mafia
        public ActionResult Index()
        {
            return View();
        }

        // GET: Mafia/Details/5
        public ActionResult Details(int id)
        {
            return View();
        }

        // GET: Mafia/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Mafia/CreateGame
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateGame(IFormCollection collection)
        {
            try
            {
                Game game = new Game();

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddPlayer(IFormCollection collection)
        {
            // Add player to game
        }

        // GET: Mafia/Edit/5
        public ActionResult Edit(int id)
        {
            return View();
        }

        // POST: Mafia/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, IFormCollection collection)
        {
            try
            {
                // TODO: Add update logic here

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: Mafia/Delete/5
        public ActionResult Delete(int id)
        {
            return View();
        }

        // POST: Mafia/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id, IFormCollection collection)
        {
            try
            {
                // TODO: Add delete logic here

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }
    }
}