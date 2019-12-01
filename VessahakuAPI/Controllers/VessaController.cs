﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.Geometries;
using VessahakuAPI.Models;
using OsoiteGPS;

namespace VessahakuAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VessaController : ControllerBase
    {
        VessatContext db = new VessatContext();
        // GET: api/Vessa
        [HttpGet]
        public IEnumerable<Wct> Get()
        {

            var vessat = from a in db.Wct
                         select a;

            return vessat.ToList();
        }
        [HttpGet("Tiedot/{id}", Name = "Tiedot")]
        public Wct Tiedot(int id)
        {
            return db.Wct.Find(id);
        }
        [HttpGet("Kommentit/{id}", Name = "Kommentit")]
        public IEnumerable<Kommentit> Kommentti(int id)
        {
            var a = db.Kommentit.Where(i => i.WcId == id).ToList();
            return a;
        }
        [HttpPost("Kommentit/{id}", Name = "Kommentit")]
        public IActionResult Kommentti(int id, Kommentit k)
        {
            try
            {
                var a=new Kommentit { Arvio = k.Arvio, Sisältö = k.Sisältö, WcId = id };
                db.Kommentit.Add(a);
                db.SaveChanges();
                return CreatedAtAction("Tiedot", "Vessa", new { id = k.WcId }, k);
            }
            catch
            {
                return BadRequest();

            }
             
        }

        // GET: api/Vessa/5
        [HttpGet("Haku/{nimi}", Name = "Haku")]
        public IEnumerable<Wct> GetNimellä(string nimi)
        {

            var a = db.Wct.Where(s => s.Nimi.ToLower().Contains(nimi.ToLower())).ToList();
            return a;
        }
        [HttpGet("Haku/{pnro}", Name = "Hakupostin")]
        public IEnumerable<Wct> Getpnrolla(string pnro)
        {
            var a = db.Wct.Where(s => s.Postinro.Contains(pnro)).ToList();
            return a;
        }

        [HttpGet("Haku/{longi}/{lat}", Name = "Hakuetäisyydellä")]
        public IEnumerable<Wct> Getpnrolla(decimal longi, decimal lat)
        {
            Coordinate c = new Coordinate(Convert.ToDouble(longi), Convert.ToDouble(lat));
            var a = db.Wct.OrderBy(s => s.Sijainti.Distance(new Point(c) { SRID = 4326 })).ToList();

            return a;
        }

        [HttpGet("Lahimmat/{lat}/{lon}", Name = "Lähimmät sijainnista")]
        public object LähimmätSijainnista(string lat, string lon, int? maara, string postinumero, string kaupunki)
        {
            var sijainti = new Point(Convert.ToDouble(lon), Convert.ToDouble(lat)) { SRID = 4326 };
            var osoite = Osoite.SijainninPerusteella(Convert.ToDecimal(lat), Convert.ToDecimal(lon));
            return new { osoite = osoite, vessat = LähimmätSijainnista(sijainti, maara, postinumero, kaupunki) };
        }
        private IEnumerable<Wct> LähimmätSijainnista(Point sijainti, int? määrä, string postinumero, string kaupunki)
        {
            var lista = db.Wct.OrderBy(wc => wc.Sijainti.Distance(sijainti)).ToList();
            if (!string.IsNullOrWhiteSpace(postinumero))
            {
                lista = lista.Where(wc => wc.Postinro == postinumero.Trim()).ToList();
            }
            else if (!string.IsNullOrWhiteSpace(kaupunki))
            {
                lista = lista.Where(wc => wc.Kaupunki.ToLower() == kaupunki.Trim().ToLower()).ToList();
            }
            if (määrä != null)
            {
                return lista.Take(määrä.GetValueOrDefault());
            }
            else
            {
                return lista;
            }
        }

        [HttpGet("Lahimmat/{paikka}", Name = "Lähimmät paikasta")]
        public object LähimmätPaikasta(string paikka, int? maara, string postinumero, string kaupunki)
        {
            var sijaintitiedot = Osoite.Haku(paikka);
            return new { osoite = sijaintitiedot.osoite, vessat = LähimmätSijainnista(sijaintitiedot.sijainti, maara, postinumero, kaupunki) };
        }

        [HttpPost]
        public IActionResult LisääWC([FromBody] Wct wc)
        {
            try
            {
                var uusi = new Wct();
                uusi.Nimi = SiistiRivi(wc.Nimi);
                uusi.Katuosoite = SiistiRivi(wc.Katuosoite);
                uusi.Kaupunki = SiistiRivi(wc.Kaupunki);
                try
                {
                    uusi.Postinro = Osoite.Postinumero(uusi.Katuosoite, uusi.Kaupunki);
                }
                catch (ArgumentException)
                {
                    if (wc.Postinro.Length == 5 && wc.Postinro.ToCharArray().All(c => char.IsDigit(c)))
                    {
                        uusi.Postinro = wc.Postinro;
                    }
                    else
                    {
                        return BadRequest();
                    }
                }
                var sijainti = Osoite.Haku(uusi.Katuosoite, uusi.Postinro, uusi.Kaupunki).Item2;
                uusi.Lat = Convert.ToDecimal(sijainti.Coordinates.First().Y);
                uusi.Long = Convert.ToDecimal(sijainti.Coordinates.First().X);
                uusi.Sijainti = sijainti;
                uusi.Ilmainen = wc.Ilmainen;
                uusi.Unisex = wc.Unisex;
                uusi.Saavutettava = wc.Saavutettava;
                uusi.Aukioloajat = SiistiRivi(wc.Aukioloajat);
                uusi.Koodi = wc.Koodi?.Trim();
                uusi.Ohjeet = SiistiRivi(wc.Ohjeet);
                db.Wct.Add(uusi);
                db.SaveChanges();
                return CreatedAtAction("Tiedot", "Vessa", new { id = uusi.WcId }, uusi);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message); // Poista e.Message ennen tuotantoa
            }
        }

        // PUT: api/Vessa/5
        [HttpPost("{id}")]
        public IActionResult Put(int id, [FromBody] Wct wc)
        {
            try
            {
                var muutettava = db.Wct.Find(id);
                muutettava.Nimi = SiistiRivi(wc.Nimi);
                muutettava.Katuosoite = SiistiRivi(wc.Katuosoite);
                muutettava.Kaupunki = SiistiRivi(wc.Kaupunki);
                try
                {
                    muutettava.Postinro = Osoite.Postinumero(muutettava.Katuosoite, muutettava.Kaupunki);
                }
                catch (ArgumentException)
                {
                    if (wc.Postinro.Length == 5 && wc.Postinro.ToCharArray().All(c => char.IsDigit(c)))
                    {
                        muutettava.Postinro = wc.Postinro;

                    }
                    else
                    {
                        return BadRequest();
                    }
                }
                var sijainti = Osoite.Haku(muutettava.Katuosoite, muutettava.Postinro, muutettava.Kaupunki).Item2;
                muutettava.Lat = Convert.ToDecimal(sijainti.Coordinates.First().Y);
                muutettava.Long = Convert.ToDecimal(sijainti.Coordinates.First().X);
                muutettava.Sijainti = sijainti;
                muutettava.Ilmainen = wc.Ilmainen;
                muutettava.Unisex = wc.Unisex;
                muutettava.Saavutettava = wc.Saavutettava;
                muutettava.Aukioloajat = SiistiRivi(wc.Aukioloajat);
                muutettava.Koodi = wc.Koodi?.Trim();
                muutettava.Ohjeet = SiistiRivi(wc.Ohjeet);
                muutettava.Muokattu = DateTime.Now;
                db.Update(muutettava);
                db.SaveChanges();
                return Ok();
            }
            catch (Exception)
            {
                return BadRequest();
            }
        }

        private string SiistiRivi(string teksti)
        {
            if (!string.IsNullOrEmpty(teksti))
            {
                var trimmattu = teksti.Trim();
                return trimmattu.Substring(0, 1).ToUpper() + trimmattu.Substring(1);
            }
            else
            {
                return teksti;
            }
        }

        //// DELETE: api/ApiWithActions/5
        //[HttpDelete("{id}")]
        //public void Delete(int id)
        //{
        //}
    }
}
