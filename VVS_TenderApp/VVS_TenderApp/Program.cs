// See https://aka.ms/new-console-template for more information
using System.Reflection;
using VVS_TenderApp.Data;
using VVS_TenderApp.Models;
using VVS_TenderApp.Services;


namespace VVS_TenderApp
{
    class Program
    {
        static DbClass db = new DbClass();

        static TenderService tenderService = new TenderService(db);
        static PonudaService ponudaService = new PonudaService(db);
        static AuthService authService = new AuthService(db);
        static PretragaService pretragaService = new PretragaService(db);
        static RangiranjeService rangiranjeService = new RangiranjeService(db);


        static Korisnik ulogovanKorisnik = null;

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Console.WriteLine("--SISTEM ZA UPRAVLJANJE TENDERIMA--");
            Console.WriteLine();
            Console.WriteLine("Pritisnite bilo koji taster za nastavak...");
            Console.ReadKey();

            while (true)
            {
                try
                {
                    if (ulogovanKorisnik == null)
                    {
                        PocetniMeni();
                    }
                    else
                    {
                        if (ulogovanKorisnik.Uloga == Uloga.Admin)
                            AdministratorMeni();
                        else
                            FirmaMeni();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\nNeočekivana greška: {ex.Message}");
                    Console.WriteLine("Pritisnite bilo koji taster za nastavak...");
                    Console.ReadKey();
                }
            }
        }


        static void PocetniMeni()
        {
            Console.Clear();
            Console.WriteLine("--DOBRODOŠLI--");
            Console.WriteLine();
            Console.WriteLine("1. Prijava");
            Console.WriteLine("2. Registracija firme");
            Console.WriteLine("3. Pregled tendera");
            Console.WriteLine("0. Izlaz");
            Console.WriteLine();
            Console.Write("Izbor: ");

            string izbor = Console.ReadLine();

            switch (izbor)
            {
                case "1":
                    Prijava();
                    break;
                case "2":
                    RegistracijaFirme();
                    break;
                case "3":
                    PregledTendera();
                    break;
                case "0":
                    Console.WriteLine("\nHvala što ste koristili sistem. Doviđenja!");
                    Environment.Exit(0);
                    break;
                default:
                    Console.WriteLine("\nNevažeća opcija!");
                    Console.ReadKey();
                    break;
            }
        }

        static void Prijava()
        {
            Console.Clear();
            Console.WriteLine("--PRIJAVA--");
            Console.WriteLine();

            Console.Write("Email: ");
            string email = Console.ReadLine();

            Console.Write("Lozinka: ");
            string lozinka = ProcitajLozinku();

            try
            {
                ulogovanKorisnik = authService.Prijava(email, lozinka);

                Console.WriteLine($"\nUspješna prijava! Dobrodošli, {ulogovanKorisnik.Ime}!");

                if (ulogovanKorisnik.FirmaId.HasValue)
                {
                    var firma = db.DohvatiFirmu(ulogovanKorisnik.FirmaId.Value);
                    Console.WriteLine($"Firma: {firma.Naziv}");
                }

                Console.WriteLine("\nPritisnite bilo koji taster...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nGreška: {ex.Message}");
                Console.WriteLine("Pritisnite bilo koji taster...");
                Console.ReadKey();
            }
        }

        static void RegistracijaFirme()
        {
            Console.Clear();
            Console.WriteLine("--REGISTRACIJA FIRME--");
            Console.WriteLine();

            try
            {
                Console.WriteLine("--- PODACI O FIRMI ---\n");
                Console.Write("Naziv firme: ");
                string nazivFirme = Console.ReadLine();

                Console.Write("PIB (9 cifara): ");
                string pib = Console.ReadLine();

                Console.Write("Adresa: ");
                string adresa = Console.ReadLine();

                Console.Write("Email firme: ");
                string emailFirme = Console.ReadLine();

                Console.Write("Telefon: ");
                string telefon = Console.ReadLine();

                Console.WriteLine("\n--- PODACI KORISNIKA (direktora) ---\n");

                Console.Write("Ime i prezime: ");
                string imeKorisnika = Console.ReadLine();

                Console.Write("Email: ");
                string emailKorisnika = Console.ReadLine();

                Console.Write("Lozinka (min 6 karaktera sa brojem): ");
                string lozinka = ProcitajLozinku();

                Console.Write("\nPotvrdite lozinku: ");
                string lozinkaPotvrda = ProcitajLozinku();

                if (lozinka != lozinkaPotvrda)
                {
                    Console.WriteLine("\nLozinke se ne poklapaju!");
                    Console.ReadKey();
                    return;
                }
                authService.RegistracijaFirme(nazivFirme, pib, adresa, emailFirme, telefon, imeKorisnika, emailKorisnika, lozinka);

                Console.WriteLine("\nUspješna registracija!Možete se prijaviti sa unesenim podacima.");
                Console.WriteLine("\nPritisnite bilo koji taster...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nGreška: {ex.Message}");
                Console.WriteLine("Pritisnite bilo koji taster...");
                Console.ReadKey();
            }
        }

        static void PregledTendera()
        {
            Console.Clear();
            Console.WriteLine("--- PREGLED TENDERA ---\n");
            Console.WriteLine("1. Svi tenderi");
            Console.WriteLine("2. Pretrazi tendere");
            Console.WriteLine("3. Sortiraj tendere");
            Console.WriteLine("0. Nazad\n");
            Console.Write("Izbor: ");

            switch (Console.ReadLine())
            {
                case "1": PrikaziTendere(db.DohvatiAktivneTendere()); break;
                case "2": PretragaTendera(); break;
                case "3": SortiranjeTendera(); break;
                case "0": return;
                default:
                    Console.WriteLine("Nevazeca opcija!");
                    Console.ReadKey();
                    break;
            }
        }

        static void PrikaziTendere(List<Tender> tenderi)
        {
            Console.Clear();
            Console.WriteLine($"--- TENDERI ({tenderi.Count}) ---\n");

            if (tenderi.Count == 0)
            {
                Console.WriteLine("Nema tendera.");
            }
            else
            {
                foreach (var t in tenderi)
                {
                    var firma = db.DohvatiFirmu(t.FirmaId);
                    var ponude = db.DohvatiPonudePoTenderu(t.Id);

                    Console.WriteLine($"[{t.Id}] {t.Naziv}");
                    Console.WriteLine($"Narucioc: {firma.Naziv}");
                    Console.WriteLine($"Rok: {t.RokZaPrijavu:dd.MM.yyyy}");
                    Console.WriteLine($"Vrijednost: {t.ProcijenjenaVrijednost:N2} KM");
                    Console.WriteLine($"Ponude: {ponude.Count}");
                    Console.WriteLine();
                }
            }

            Console.WriteLine("Pritisnite Enter...");
            Console.ReadKey();
        }

        static void PretragaTendera()
        {
            Console.Clear();
            Console.WriteLine("--- PRETRAGA TENDERA ---\n");
            Console.Write("Kljucna rijec: ");
            string kljucna = Console.ReadLine();

            var rezultati = pretragaService.PretraziPoKljucnojRijeci(kljucna)
                .Where(t => t.Status == StatusTendera.Otvoren)
                .ToList();

            PrikaziTendere(rezultati);
        }

        static void SortiranjeTendera()
        {
            Console.Clear();
            Console.WriteLine("--- SORTIRANJE TENDERA ---\n");
            Console.WriteLine("1. Po nazivu (A-Z)");
            Console.WriteLine("2. Po vrijednosti (uzlazno)");
            Console.WriteLine("3. Po vrijednosti (silazno)");
            Console.WriteLine("4. Po roku (najskoriji)");
            Console.WriteLine("0. Nazad\n");
            Console.Write("Izbor: ");

            var tenderi = db.DohvatiAktivneTendere();
            List<Tender> sortirano = null;

            switch (Console.ReadLine())
            {
                case "1": sortirano = pretragaService.SortirajPoNazivu(tenderi, true); break;
                case "2": sortirano = pretragaService.SortirajPoVrijednosti(tenderi, true); break;
                case "3": sortirano = pretragaService.SortirajPoVrijednosti(tenderi, false); break;
                case "4": sortirano = pretragaService.SortirajPoRoku(tenderi, true); break;
                case "0": return;
                default:
                    Console.WriteLine("Nevazeca opcija!");
                    Console.ReadKey();
                    return;
            }

            PrikaziTendere(sortirano);
        }
        static void NaprednaPretragaTendera()
        {
            Console.Clear();
            Console.WriteLine("--- NAPREDNA PRETRAGA TENDERA ---\n");
            Console.WriteLine("(Enter za preskok)\n");

            try
            {
                Console.Write("Kljucna rijec: ");
                string kljucnaRijec = Console.ReadLine();

                Console.Write("Min vrijednost (KM): ");
                string minStr = Console.ReadLine();
                decimal? minVrijednost = string.IsNullOrWhiteSpace(minStr)
                    ? null
                    : decimal.Parse(minStr);

                Console.Write("Max vrijednost (KM): ");
                string maxStr = Console.ReadLine();
                decimal? maxVrijednost = string.IsNullOrWhiteSpace(maxStr)
                    ? null
                    : decimal.Parse(maxStr);

                Console.Write("Rok od (dd.MM.yyyy): ");
                string rokOdStr = Console.ReadLine();
                DateTime? rokOd = string.IsNullOrWhiteSpace(rokOdStr)
                    ? null
                    : DateTime.ParseExact(rokOdStr, "dd.MM.yyyy", null);

                Console.Write("Rok do (dd.MM.yyyy): ");
                string rokDoStr = Console.ReadLine();
                DateTime? rokDo = string.IsNullOrWhiteSpace(rokDoStr)
                    ? null
                    : DateTime.ParseExact(rokDoStr, "dd.MM.yyyy", null);

                // Defaultno samo otvoreni tenderi
                var rezultati = pretragaService.NaprednaPretraga(
                    string.IsNullOrWhiteSpace(kljucnaRijec) ? null : kljucnaRijec,
                    minVrijednost,
                    maxVrijednost,
                    StatusTendera.Otvoren,
                    rokOd,
                    rokDo, null
                );

                PrikaziTendere(rezultati);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nGreska: {ex.Message}");
                Console.ReadKey();
            }
        }

        static void FirmaMeni()
        {
            var firma = db.DohvatiFirmu(ulogovanKorisnik.FirmaId.Value);

            Console.Clear();
            Console.WriteLine($"--- FIRMA: {firma.Naziv} ---\n");
            Console.WriteLine();
            Console.WriteLine("MOJI TENDERI:");
            Console.WriteLine("1. Raspiši novi tender");
            Console.WriteLine("2. Pregled mojih tendera");
            Console.WriteLine("3. Uredi tender");
            Console.WriteLine("4. Obriši tender");
            Console.WriteLine("5. Zatvori tender");
            Console.WriteLine("6. Dodijeli tender\n");

            Console.WriteLine("PONUDE:");
            Console.WriteLine("7. Pregled tendera");
            Console.WriteLine("8. Prijavi se na tender");
            Console.WriteLine("9. Moje ponude");
            Console.WriteLine("10. Uredi ponudu");
            Console.WriteLine("11. Obrisi ponudu\n");
            Console.WriteLine();
            Console.WriteLine("0. Odjava");
            Console.WriteLine("12. Izlaz");
            Console.WriteLine();
            Console.Write("Izbor: ");

            switch (Console.ReadLine())
            {
                case "1": RaspisivanjeTendera(); break;
                case "2": MojiTenderi(); break;
                case "3": UredjivanjeTendera(); break;
                case "4": BrisanjeMogTendera(); break;
                case "5": ZatvaranjeTendera(); break;
                case "6": DodjelaTendera(); break;
                case "7": PregledTendera(); break;
                case "8": PrijavaNaTender(); break;
                case "9": MojePonude(); break;
                case "10": UredjivanjeMojePonude(); break;
                case "11": BrisanjeMojePonude(); break;
                case "0":
                    ulogovanKorisnik = null;
                    Console.WriteLine("Uspjesno odjavljivanje.");
                    Console.ReadKey();
                    break;
                case "12":
                    Console.WriteLine("\nHvala što ste koristili sistem. Doviđenja!");
                    Environment.Exit(0);break;
                default:
                    Console.WriteLine("Nevazeca opcija!");
                    Console.ReadKey();
                    break;
            }
        }

        static void RaspisivanjeTendera()
        {
            Console.Clear();
            Console.WriteLine("--RASPISIVANJE TENDERA--\n");
            Console.WriteLine();

            try
            {
                Console.Write("Naziv: ");
                string naziv = Console.ReadLine();

                Console.Write("Opis: ");
                string opis = Console.ReadLine();

                Console.Write("Rok (dd.MM.yyyy): ");
                DateTime rok = DateTime.ParseExact(Console.ReadLine(), "dd.MM.yyyy", null);

                Console.Write("Vrijednost (KM): ");
                decimal vrijednost = decimal.Parse(Console.ReadLine());


                var kriteriji = new List<Kriterij>();

                Console.WriteLine("\nUnesite težine kriterija u postotcima (zbir treba biti 1,0):\n");

                foreach (TipKriterija tip in Enum.GetValues(typeof(TipKriterija)))
                {
                    Console.Write(tip.ToReadableString() + " (0-1,0): ");
                    decimal tezina = decimal.Parse(Console.ReadLine());
                    kriteriji.Add(new Kriterij { Tip = tip, Tezina = tezina });
                }

                tenderService.ValidirajIKreirajTender(ulogovanKorisnik.FirmaId.Value, naziv, opis, rok, vrijednost, kriteriji);

                Console.WriteLine("\nTender uspjesno kreiran!");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nGreska: {ex.Message}");
                Console.ReadKey();
            }
        }

        static void MojiTenderi()
        {
            Console.Clear();
            Console.WriteLine("--MOJI TENDERI--");
            Console.WriteLine();
            try
            {
                var tenderi = tenderService.DohvatiMojeTendere(ulogovanKorisnik.FirmaId.Value);

                if (tenderi.Count == 0)
                {
                    Console.WriteLine("Nemate raspisanih tendera.");
                }
                else
                {
                    Console.WriteLine($"Ukupno tendera: {tenderi.Count}\n");

                    foreach (var tender in tenderi)
                    {
                        var ponude = rangiranjeService.RangirajPonude(tender.Id);

                        Console.WriteLine($"[ID: {tender.Id}] {tender.Naziv}");
                        Console.WriteLine($"Rok: {tender.RokZaPrijavu:dd.MM.yyyy}");
                        Console.WriteLine($"Vrijednost: {tender.ProcijenjenaVrijednost:N2} KM");
                        Console.WriteLine($"Status: {tender.Status}");
                        Console.WriteLine($"Broj ponuda: {ponude.Count}");

                        if (ponude.Count > 0)
                        {
                            Console.WriteLine("  Ponude:");
                            foreach (var ponuda in ponude.Take(3))
                            {
                                var firmaP = db.DohvatiFirmu(ponuda.ponuda.FirmaId);
                                Console.WriteLine($"    • {firmaP.Naziv}: {ponuda.ponuda.Iznos:N2} KM");
                            }
                        }

                        Console.WriteLine("─────────────────────────────────────────");
                    }
                }
                Console.WriteLine("\nPritisnite bilo koji taster...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nGreska: {ex.Message}");
                Console.ReadKey();
            }
        }
        static void UredjivanjeTendera()
        {
            Console.Clear();
            Console.WriteLine("--UREDJIVANJE TENDERA--\n");
            try
            {
                var tenderi = tenderService.DohvatiMojeTendere(ulogovanKorisnik.FirmaId.Value)
                    .Where(t => t.Status == StatusTendera.Otvoren).ToList();

                if (tenderi.Count == 0)
                {
                    Console.WriteLine("Nemate otvorenih tendera.");
                    Console.ReadKey();
                    return;
                }

                foreach (var t in tenderi)
                    Console.WriteLine($"[{t.Id}] {t.Naziv}");

                Console.Write("\nID tendera: ");
                int id = int.Parse(Console.ReadLine());

                var tender = db.DohvatiTender(id);

                if (tender.FirmaId != ulogovanKorisnik.FirmaId.Value)
                {
                    Console.WriteLine("Ne mozete urediti tudji tender!");
                    Console.ReadKey();
                    return;
                }

                Console.Write("Novi naziv (Enter za preskok): ");
                string naziv = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(naziv)) naziv = tender.Naziv;

                Console.Write("Novi opis (Enter za preskok): ");
                string opis = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(opis)) opis = tender.Opis;

                Console.Write("Novi rok (dd.MM.yyyy, Enter za preskok): ");
                string rokStr = Console.ReadLine();
                DateTime rok = string.IsNullOrWhiteSpace(rokStr)
                    ? tender.RokZaPrijavu
                    : DateTime.ParseExact(rokStr, "dd.MM.yyyy", null);

                Console.Write("Nova vrijednost (KM, Enter za preskok): ");
                string vrijednostStr = Console.ReadLine();
                decimal vrijednost = string.IsNullOrWhiteSpace(vrijednostStr)
                    ? tender.ProcijenjenaVrijednost
                    : decimal.Parse(vrijednostStr);

                tenderService.AzurirajTender(id, ulogovanKorisnik.FirmaId.Value, naziv, opis, rok, vrijednost);

                Console.WriteLine("\nTender uspjesno azuriran!");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nGreska: {ex.Message}");
                Console.ReadKey();
            }
        }
        static void BrisanjeMogTendera()
        {
            Console.Clear();
            Console.WriteLine("--BRISANJE TENDERA--\n");
            try
            {
                var tenderi = tenderService.DohvatiMojeTendere(ulogovanKorisnik.FirmaId.Value);

                if (tenderi.Count == 0)
                {
                    Console.WriteLine("Nemate tendera.");
                    Console.ReadKey();
                    return;
                }

                foreach (var t in tenderi)
                    Console.WriteLine($"[{t.Id}] {t.Naziv}");

                Console.Write("\nID tendera: ");
                int id = int.Parse(Console.ReadLine());

                var tender = db.DohvatiTender(id);

                if (tender.FirmaId != ulogovanKorisnik.FirmaId.Value)
                {
                    Console.WriteLine("Ne mozete obrisati tudji tender!");
                    Console.ReadKey();
                    return;
                }
                Console.Write("Sigurni ste? (da/ne): ");
                if (Console.ReadLine()?.ToLower() == "da")
                {
                    tenderService.ObrisiTender(id);
                    Console.WriteLine("Tender obrisan!");
                }

                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nGreska: {ex.Message}");
                Console.ReadKey();
            }
        }
        static void ZatvaranjeTendera()
        {
            Console.Clear();
            Console.WriteLine("--ZATVARANJE TENDERA--\n");
            Console.WriteLine();
            try
            {
                var tenderi = tenderService.DohvatiMojeTendere(ulogovanKorisnik.FirmaId.Value)
                    .Where(t => t.Status == StatusTendera.Otvoren).ToList();

                if (tenderi.Count == 0)
                {
                    Console.WriteLine("Nemate otvorenih tendera.");
                    Console.ReadKey();
                    return;
                }

                foreach (var t in tenderi)
                {
                    Console.WriteLine($"[{t.Id}] {t.Naziv} - Rok: {t.RokZaPrijavu:dd.MM.yyyy}");
                }

                Console.Write("\nID tendera za zatvaranje: ");
                int tenderId = int.Parse(Console.ReadLine());

                tenderService.ZatvoriTender(tenderId, ulogovanKorisnik.FirmaId.Value);
                Console.WriteLine("\nTender uspješno zatvoren!");
                Console.WriteLine("Pritisnite bilo koji taster...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nGreska: {ex.Message}");
                Console.ReadKey();
            }
        }

        static void DodjelaTendera()
        {
            Console.Clear();
            Console.WriteLine("--DODJELA TENDERA--\n");
            Console.WriteLine();
            try
            {
                var tenderi = tenderService.DohvatiMojeTendere(ulogovanKorisnik.FirmaId.Value)
                    .Where(t => t.Status == StatusTendera.Zatvoren) .ToList();

                if (tenderi.Count == 0)
                {
                    Console.WriteLine("Nemate zatvorenih tendera za dodjelu.");
                    Console.ReadKey();
                    return;
                }
                Console.WriteLine("ZATVORENI TENDERI:\n");
                foreach (var t in tenderi)
                {
                    var ponude = db.DohvatiPonudePoTenderu(t.Id);
                    Console.WriteLine($"[{t.Id}] {t.Naziv} - Ponuda: {ponude.Count}");
                }

                Console.Write("\nID tendera: ");
                int tenderId = int.Parse(Console.ReadLine());

                var tender = db.DohvatiTender(tenderId);
                var svePonude = db.DohvatiPonudePoTenderu(tenderId);

                if (svePonude.Count == 0)
                {
                    Console.WriteLine("\nNema ponuda za ovaj tender!");
                    Console.ReadKey();
                    return;
                }

                // 3) Izbor načina dodjele
                Console.WriteLine("\nOdaberite način dodjele:");
                Console.WriteLine("1 - Pregled ponuda i ručna dodjela");
                Console.WriteLine("2 - Automatska dodjela po rangiranju");
                Console.Write("\nVaš odabir: ");

                if (!int.TryParse(Console.ReadLine(), out int zeljenaOpcija) ||
                    (zeljenaOpcija != 1 && zeljenaOpcija != 2))
                {
                    Console.WriteLine("Neispravan odabir opcije.");
                    Console.ReadKey();
                    return;
                }

                if (zeljenaOpcija == 1)
                {
                    // --- RUČNA DODJELA ---

                    Console.WriteLine("\nPONUDE (sortirane po cijeni):\n");
                    foreach (var p in svePonude.OrderBy(p => p.Iznos))
                    {
                        var firmaP = db.DohvatiFirmu(p.FirmaId);
                        string nazivFirme = firmaP != null ? firmaP.Naziv : $"Firma {p.FirmaId}";
                        Console.WriteLine($"[{p.Id}] {nazivFirme}: {p.Iznos:N2} KM");
                    }

                    Console.Write("\nUnesite ID pobjedničke ponude: ");
                    if (!int.TryParse(Console.ReadLine(), out int ponudaId))
                    {
                        Console.WriteLine("Neispravan unos ID-a ponude.");
                        Console.ReadKey();
                        return;
                    }

                    tenderService.DodijeliTender(tenderId, ponudaId, ulogovanKorisnik.FirmaId.Value);

                    Console.WriteLine("\nTender uspješno dodijeljen (ručna dodjela)!");
                    Console.WriteLine("Pritisnite bilo koji taster za povratak...");
                    Console.ReadKey();
                }
                else if (zeljenaOpcija == 2)
                {
                    // --- AUTOMATSKA DODJELA PO RANGIRANJU ---

                    var pobjednickaPonuda = tenderService.AutomatskiDodijeliTender(
                        tenderId,
                        ulogovanKorisnik.FirmaId.Value);

                    var pobjednickaFirma = db.DohvatiFirmu(pobjednickaPonuda.FirmaId);

                    Console.WriteLine("\nTender je automatski dodijeljen na osnovu rangiranja ponuda.");
                    Console.WriteLine($"Pobjednička firma: {pobjednickaFirma?.Naziv ?? pobjednickaPonuda.FirmaId.ToString()}");
                    Console.WriteLine($"Iznos ponude: {pobjednickaPonuda.Iznos:N2} KM");
                    Console.WriteLine($"Status tendera: {tender.Status}");

                    Console.WriteLine("\nPritisnite bilo koji taster za povratak...");
                    Console.ReadKey();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nGreska: {ex.Message}");
                Console.ReadKey();
            }
        }

        static void PrijavaNaTender()
        {
            Console.Clear();
            Console.WriteLine("--PRIJAVA NA TENDER--\n");
            Console.WriteLine();

            try
            {
                var tenderi = tenderService.DohvatiTudjeTendere(ulogovanKorisnik.FirmaId.Value);

                if (tenderi.Count == 0)
                {
                    Console.WriteLine("Nema dostupnih tendera za prijavu.");
                    Console.ReadKey();
                    return;
                }
                Console.WriteLine("DOSTUPNI TENDERI:\n");
                foreach (var t in tenderi)
                {
                    var narucioc = db.DohvatiFirmu(t.FirmaId);
                    Console.WriteLine($"[{t.Id}] {t.Naziv} - {narucioc.Naziv}");
                    Console.WriteLine($"    Rok: {t.RokZaPrijavu:dd.MM.yyyy}");
                    Console.WriteLine($"    Procijenjena: {t.ProcijenjenaVrijednost:N2} KM");
                    Console.WriteLine();
                }

                Console.Write("ID tendera: ");
                int tenderId = int.Parse(Console.ReadLine());

                Console.Write("Vaša ponuđena cijena (KM): ");
                string iznosStr = Console.ReadLine();
                decimal iznos = string.IsNullOrWhiteSpace(iznosStr)
                    ? 0
                    : decimal.Parse(iznosStr);

                ponudaService.ValidirajIPosaljiPonudu(tenderId, ulogovanKorisnik.FirmaId.Value, iznos);
                
                Console.WriteLine("\nPonuda uspješno poslata!");
                Console.WriteLine("Pritisnite bilo koji taster...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nGreška: {ex.Message}");
                Console.ReadKey();
            }
        }

        static void MojePonude()
        {
            Console.Clear();
            Console.WriteLine("--MOJE PONUDE--\n");
            Console.WriteLine();
            try
            {
                var ponude = ponudaService.DohvatiMojePonude(ulogovanKorisnik.FirmaId.Value);

                if (ponude.Count == 0)
                {
                    Console.WriteLine("Nemate poslanih ponuda.");
                }
                else
                {
                    Console.WriteLine($"Ukupno ponuda: {ponude.Count}\n");

                    foreach (var ponuda in ponude)
                    {
                        var tender = db.DohvatiTender(ponuda.TenderId);
                        var narucioc = db.DohvatiFirmu(tender.FirmaId);
                        Console.WriteLine($"[ID: {ponuda.Id}] Tender: {tender.Naziv}");
                        Console.WriteLine($"Naručilac: {narucioc.Naziv}");
                        Console.WriteLine($"Vaša ponuda: {ponuda.Iznos:N2} KM");
                        Console.WriteLine($"Status ponude: {ponuda.Status.ToReadableString()}");
                        Console.WriteLine($"Datum slanja: {ponuda.DatumSlanja:dd.MM.yyyy}");
                        Console.WriteLine($"Ocjena ponude (0-1): {rangiranjeService.DajOcjenuZaPonudu(ponuda):0.00}");
                        Console.WriteLine("─────────────────────────────────────────");
                    }
                }

                Console.WriteLine("\nPritisnite bilo koji taster...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nGreska: {ex.Message}");
                Console.ReadKey();
            }
        }
        static void UredjivanjeMojePonude()
        {
            Console.Clear();
            Console.WriteLine("--UREDJIVANJE PONUDE--\n");
            try
            {
                var ponude = ponudaService.DohvatiMojePonude(ulogovanKorisnik.FirmaId.Value)
                    .Where(p => p.Status == StatusPonude.NaCekanju).ToList();

                if (ponude.Count == 0)
                {
                    Console.WriteLine("Nemate ponuda na čekanju.");
                    Console.ReadKey();
                    return;
                }
                foreach (var p in ponude)
                {
                    var tender = db.DohvatiTender(p.TenderId);
                    Console.WriteLine($"[ID: {p.Id}]  Tender {tender.Naziv} - {p.Iznos:N2} KM");
                }

                Console.Write("\nID ponude: ");
                int id = int.Parse(Console.ReadLine());

                var ponuda = db.DohvatiPonudu(id);

                if (ponuda.FirmaId != ulogovanKorisnik.FirmaId.Value)
                {
                    Console.WriteLine("Ne možete urediti tuđu ponudu!");
                    Console.ReadKey();
                    return;
                }

                Console.Write("Novi iznos (KM): ");
                string iznosStr = Console.ReadLine();
                decimal iznos = string.IsNullOrWhiteSpace(iznosStr) 
                    ? 0 :decimal.Parse(iznosStr);

                ponudaService.AzurirajPonudu(id, ulogovanKorisnik.FirmaId.Value, iznos);

                Console.WriteLine("\nPonuda ažurirana!");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nGreska: {ex.Message}");
                Console.ReadKey();
            }
        }

        static void BrisanjeMojePonude()
        {
            Console.Clear();
            Console.WriteLine("--BRISANJE PONUDE--\n");
            try
            {
                var ponude = ponudaService.DohvatiMojePonude(ulogovanKorisnik.FirmaId.Value)
                    .Where(p => p.Status == StatusPonude.NaCekanju).ToList();

                if (ponude.Count == 0)
                {
                    Console.WriteLine("Nemate ponuda na čekanju.");
                    Console.ReadKey();
                    return;
                }

                foreach (var p in ponude)
                {
                    var tender = db.DohvatiTender(p.TenderId);
                    Console.WriteLine($"[UD: {p.Id}] Tender {tender.Naziv} - {p.Iznos:N2} KM");
                }

                Console.Write("\nID ponude: ");
                int id = int.Parse(Console.ReadLine());

                var ponuda = db.DohvatiPonudu(id);

                if (ponuda.FirmaId != ulogovanKorisnik.FirmaId.Value)
                {
                    Console.WriteLine("Ne možete obrisati tuđu ponudu!");
                    Console.ReadKey();
                    return;
                }

                Console.Write("Sigurni ste? (da/ne): ");
                if (Console.ReadLine()?.ToLower() == "da")
                {
                    ponudaService.PovuciPonudu(id, ulogovanKorisnik.FirmaId.Value);
                    Console.WriteLine("Ponuda obrisana!");
                }

                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nGreska: {ex.Message}");
                Console.ReadKey();
            }
        }

        static void AdministratorMeni()
        {
            Console.Clear();
            Console.WriteLine("--ADMIN PANEL--\n");
            Console.WriteLine();
            Console.WriteLine("1. Pregled svih tendera");
            Console.WriteLine("2. Obriši tender");
            Console.WriteLine("3. Raspisi tender");
            Console.WriteLine();
            Console.WriteLine("0. Odjava");
            Console.WriteLine("5. Izlaz");
            Console.WriteLine();
            Console.Write("Izbor: ");

            switch (Console.ReadLine())
            {
                case "1": PregledTendera(); break;
                case "2":AdminBrisanjeTendera(); break;
                case "3":AdminRaspisivanjeTendera(); break;
                case "0":
                    ulogovanKorisnik = null;
                    Console.WriteLine("\nUspješno ste se odjavili.");
                    Console.ReadKey();
                    break;
                case "5":
                    Console.WriteLine("\nHvala što ste koristili sistem. Doviđenja!");
                    Environment.Exit(0);
                    break;
                default:
                    Console.WriteLine("\nNevažeća opcija!");
                    Console.ReadKey();
                    break;
            }
        }
        static void AdminBrisanjeTendera()
        {
            Console.Clear();
            Console.WriteLine("--BRISANJE TENDERA--\n");
            Console.WriteLine();
            try
            {
                var tenderi = db.DohvatiSveTendere();

                if (tenderi.Count == 0)
                {
                    Console.WriteLine("Nema tendera u sistemu.");
                    Console.ReadKey();
                    return;
                }
                Console.WriteLine("SVI TENDERI:\n");
                foreach (var t in tenderi)
                {
                    var firma = db.DohvatiFirmu(t.FirmaId);
                    Console.WriteLine($"[{t.Id}] {t.Naziv} - {firma.Naziv} ({t.Status})");
                }

                Console.Write("\nID tendera za brisanje: ");
                int tenderId = int.Parse(Console.ReadLine());

                Console.Write("Da li ste sigurni? (da/ne): ");
                string potvrda = Console.ReadLine()?.ToLower();

                if (potvrda == "da")
                {
                    tenderService.ObrisiTender(tenderId);
                    Console.WriteLine("\nTender uspješno obrisan!");
                }
                else
                {
                    Console.WriteLine("\nBrisanje otkazano.");
                }

                Console.WriteLine("Pritisnite bilo koji taster...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nGreska {ex.Message}");
                Console.ReadKey();
            }
        }
        static void AdminRaspisivanjeTendera()
        {
            Console.Clear();
            Console.WriteLine("--RASPISIVANJE TENDERA (ADMIN)--\n");

            try
            {
                var firme = db.DohvatiSveFirme();

                Console.WriteLine("FIRME:\n");
                foreach (var f in firme)
                    Console.WriteLine($"[{f.Id}] {f.Naziv}");

                Console.Write("\nID firme narucioca: ");
                int firmaId = int.Parse(Console.ReadLine());

                Console.Write("Naziv: ");
                string naziv = Console.ReadLine();

                Console.Write("Opis: ");
                string opis = Console.ReadLine();

                Console.Write("Rok (dd.MM.yyyy): ");
                DateTime rok = DateTime.ParseExact(Console.ReadLine(), "dd.MM.yyyy", null);

                Console.Write("Vrijednost (KM): ");
                decimal vrijednost = decimal.Parse(Console.ReadLine());

                var kriteriji = new List<Kriterij>();

                Console.WriteLine("\nUnesite težine kriterija (zbir treba biti ili 1.0):\n");

                foreach (TipKriterija tip in Enum.GetValues(typeof(TipKriterija)))
                {
                    Console.Write($"{tip} (0-1): ");
                    decimal tezina = decimal.Parse(Console.ReadLine());
                    kriteriji.Add(new Kriterij { Tip = tip, Tezina = tezina });
                }

                tenderService.ValidirajIKreirajTender(firmaId, naziv, opis, rok, vrijednost, kriteriji);

                Console.WriteLine("\nTender kreiran!");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nGreska: {ex.Message}");
                Console.ReadKey();
            }
        }


        static string ProcitajLozinku()
        {
            string lozinka = "";
            ConsoleKeyInfo key;

            do
            {
                key = Console.ReadKey(true);

                if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
                {
                    lozinka += key.KeyChar;
                    Console.Write("*");
                }
                else if (key.Key == ConsoleKey.Backspace && lozinka.Length > 0)
                {
                    lozinka = lozinka.Substring(0, lozinka.Length - 1);
                    Console.Write("\b \b");
                }
            }
            while (key.Key != ConsoleKey.Enter);

            Console.WriteLine();
            return lozinka;
        }

    }
}
