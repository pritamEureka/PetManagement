-- =============================================================================
-- Pawzaroo demo-data seed (SQL).
--
-- Populates the user-facing pages (feed, pets, adoption, vets, store, services)
-- with realistic data, keyed off the demo users created by DemoDataSeeder.
--
-- Idempotent: re-running is a no-op once the rows exist (uniqueness keys:
-- stores.owner_user_id, doctors.license_number, products.sku, etc).
--
-- Run with:
--   docker exec -i pawzaroo-postgres-1 psql -U pawzaroo -d pawzaroo \
--     < deploy/seed-demo.sql
-- =============================================================================

BEGIN;

DO $$
DECLARE
  v_now timestamptz := now();

  -- Users (looked up by email; assumed to exist from DemoDataSeeder's first SaveChanges).
  u_maya     uuid := (SELECT id FROM users WHERE email = 'maya.rahman@example.com');
  u_arif     uuid := (SELECT id FROM users WHERE email = 'arif.hasan@example.com');
  u_liza     uuid := (SELECT id FROM users WHERE email = 'liza.akter@example.com');
  u_nusrat   uuid := (SELECT id FROM users WHERE email = 'dr.nusrat@example.com');
  u_tanvir   uuid := (SELECT id FROM users WHERE email = 'tanvir.petmart@example.com');
  u_rima     uuid := (SELECT id FROM users WHERE email = 'rima.grooming@example.com');
  u_omar     uuid := (SELECT id FROM users WHERE email = 'omar.breeder@example.com');
  u_admin    uuid := (SELECT id FROM users WHERE email = 'admin.farhana@example.com');

  -- Catalog lookups (seeded by CatalogSeeder).
  c_food        uuid := (SELECT id FROM product_categories WHERE slug = 'food');
  c_accessories uuid := (SELECT id FROM product_categories WHERE slug = 'accessories');
  c_grooming    uuid := (SELECT id FROM product_categories WHERE slug = 'grooming');
  c_toys        uuid := (SELECT id FROM product_categories WHERE slug = 'toys');
  c_treats      uuid := (SELECT id FROM product_categories WHERE slug = 'treats');

  b_royal       uuid := (SELECT id FROM brands WHERE name = 'Royal Tail');
  b_basics      uuid := (SELECT id FROM brands WHERE name = 'Pawzaroo Basics');
  b_furnation   uuid := (SELECT id FROM brands WHERE name = 'FurNation');
  b_whiskers    uuid := (SELECT id FROM brands WHERE name = 'Whiskers & Co');

  s_general     uuid := (SELECT id FROM specialties WHERE slug = 'general');
  s_dermatology uuid := (SELECT id FROM specialties WHERE slug = 'dermatology');

  -- Pre-generated UUIDs so we can wire children to parents in one go.
  pet_milo  uuid := gen_random_uuid();
  pet_buddy uuid := gen_random_uuid();
  pet_luna  uuid := gen_random_uuid();
  pet_koko  uuid := gen_random_uuid();

  store_green uuid := gen_random_uuid();
  prod_food   uuid := gen_random_uuid();
  prod_leash  uuid := gen_random_uuid();
  prod_shamp  uuid := gen_random_uuid();
  prod_toy    uuid := gen_random_uuid();

  doc_nusrat  uuid := gen_random_uuid();

  list_luna   uuid := gen_random_uuid();
  list_tara   uuid := gen_random_uuid();

  svc_rima    uuid := gen_random_uuid();

  post1 uuid := gen_random_uuid();
  post2 uuid := gen_random_uuid();
  post3 uuid := gen_random_uuid();
  post4 uuid := gen_random_uuid();
BEGIN
  -- Guard: the demo seeder gate already inserted the 11 users. If the lookups
  -- above are NULL, we bail loudly instead of inserting orphan rows.
  IF u_maya IS NULL OR u_arif IS NULL OR u_liza IS NULL OR u_nusrat IS NULL
     OR u_tanvir IS NULL OR u_rima IS NULL OR u_omar IS NULL OR u_admin IS NULL THEN
    RAISE EXCEPTION 'Demo users not found. Ensure the API booted at least once so DemoDataSeeder inserts them.';
  END IF;

  -- ---------- PETS ---------------------------------------------------------
  INSERT INTO pets (id, owner_id, name, animal_type, breed, gender, birth_date,
                    weight_kg, color, tag_number, primary_photo_url, allergies,
                    diet_notes, is_available_for_adoption, created_at, is_deleted)
  VALUES
    (pet_milo, u_maya, 'Milo', 2, 'Domestic Shorthair', 1, v_now - interval '3 years',
     4.8, 'Grey tabby', 'CAT-DHK-0142',
     'https://images.unsplash.com/photo-1574144611937-0df059b5ef3e?auto=format&fit=crop&w=900&q=80',
     'Sensitive to fish-based foods.', 'Mostly wet food with measured dry food.',
     true, v_now, false),
    (pet_buddy, u_arif, 'Buddy', 1, 'Golden Retriever', 1, v_now - interval '2 years',
     27.4, 'Golden', 'DOG-BAN-2081',
     'https://images.unsplash.com/photo-1552053831-71594a27632d?auto=format&fit=crop&w=900&q=80',
     NULL, 'Two measured meals daily.', false, v_now, false),
    (pet_luna, u_liza, 'Luna', 2, 'Calico', 2, v_now - interval '14 months',
     3.7, 'Calico', 'RES-MIR-0097',
     'https://images.unsplash.com/photo-1589883661923-6476cb0ae9f2?auto=format&fit=crop&w=900&q=80',
     NULL, 'Wet food twice daily.', true, v_now, false),
    (pet_koko, u_omar, 'Koko', 8, 'Black Bengal', 2, v_now - interval '1 year',
     19.2, 'Black', 'FARM-SVR-0204', NULL, NULL,
     'Pasture grazing + supplements.', false, v_now, false)
  ON CONFLICT (id) DO NOTHING;

  INSERT INTO pet_photos (id, pet_id, url, caption, uploaded_at) VALUES
    (gen_random_uuid(), pet_milo,
     'https://images.unsplash.com/photo-1574144611937-0df059b5ef3e?auto=format&fit=crop&w=900&q=80',
     'Milo keeping watch from the window.', v_now),
    (gen_random_uuid(), pet_buddy,
     'https://images.unsplash.com/photo-1552053831-71594a27632d?auto=format&fit=crop&w=900&q=80',
     'Buddy after his morning walk.', v_now),
    (gen_random_uuid(), pet_luna,
     'https://images.unsplash.com/photo-1589883661923-6476cb0ae9f2?auto=format&fit=crop&w=900&q=80',
     'Luna resting after vaccination.', v_now);

  -- ---------- STORE + PRODUCTS --------------------------------------------
  -- stores.owner_user_id is UNIQUE. ON CONFLICT lets re-runs no-op safely.
  INSERT INTO stores (id, owner_user_id, name, description, logo_url, banner_url,
                      address, city, country, phone_number, email, approval_status,
                      commission_percent, created_at, is_deleted)
  VALUES (store_green, u_tanvir, 'Green Paw Supplies',
          'Curated food, litter, grooming, and enrichment products with Dhaka city delivery.',
          '/uploads/stores/green-paw-logo.png', '/uploads/stores/green-paw-banner.jpg',
          'Shop 14, Sector 7 Market', 'Dhaka', 'Bangladesh', '+8801514550505',
          'orders@greenpaw.example.com', 1, 9.5, v_now, false)
  ON CONFLICT (owner_user_id) DO NOTHING
  RETURNING id INTO store_green;

  -- If the store already existed (re-run), pick up its id.
  IF store_green IS NULL THEN
    store_green := (SELECT id FROM stores WHERE owner_user_id = u_tanvir);
  END IF;

  INSERT INTO products (id, store_id, category_id, brand_id, name, description, sku,
                        price, discount_price, stock_quantity, is_active, is_featured,
                        rating_average, rating_count, created_at, is_deleted)
  VALUES
    (prod_food, store_green, c_food, b_royal,
     'Royal Tail Indoor Cat Chicken 2kg',
     'Complete dry food for adult indoor cats with hairball support.',
     'GPS-FOOD-CAT-2KG', 2450, 2290, 42, true, true, 4.7, 31, v_now, false),
    (prod_leash, store_green, c_accessories, b_basics,
     'Reflective Comfort Dog Leash',
     'Padded handle leash with reflective stitching for evening walks.',
     'GPS-ACC-LEASH-RF', 850, NULL, 24, true, false, 4.5, 12, v_now, false),
    (prod_shamp, store_green, c_grooming, b_furnation,
     'Sensitive Skin Oatmeal Shampoo',
     'Gentle oatmeal shampoo for itchy or dry coats.',
     'GPS-GRM-OAT-500', 690, 640, 36, true, true, 4.6, 9, v_now, false),
    (prod_toy, store_green, c_toys, b_whiskers,
     'Feather Wand Interactive Cat Toy',
     'Replaceable feather attachment with bell — great for indoor play sessions.',
     'GPS-TOY-WAND-01', 380, 320, 60, true, false, 4.4, 17, v_now, false)
  ON CONFLICT (sku) DO NOTHING;

  INSERT INTO product_images (id, product_id, url, order_index) VALUES
    (gen_random_uuid(), prod_food,  'https://images.unsplash.com/photo-1589924691995-400dc9ecc119?auto=format&fit=crop&w=900&q=80', 0),
    (gen_random_uuid(), prod_leash, 'https://images.unsplash.com/photo-1601758124510-52d02ddb7cbd?auto=format&fit=crop&w=900&q=80', 0),
    (gen_random_uuid(), prod_shamp, 'https://images.unsplash.com/photo-1616190264687-b7ebf1a391cd?auto=format&fit=crop&w=900&q=80', 0),
    (gen_random_uuid(), prod_toy,   'https://images.unsplash.com/photo-1592194996308-7b43878e84a6?auto=format&fit=crop&w=900&q=80', 0);

  -- ---------- DOCTOR ------------------------------------------------------
  -- doctors.user_id and doctors.license_number are both UNIQUE.
  INSERT INTO doctors (id, user_id, license_number, specialty, experience_years,
                       about, clinic_name, clinic_address, city, country,
                       consultation_fee, consultation_type, online_available,
                       offline_available, approval_status, rating_average,
                       rating_count, auto_confirm_appointments,
                       default_slot_minutes, cancellation_cutoff_hours,
                       created_at, is_deleted)
  VALUES (doc_nusrat, u_nusrat, 'DVM-BVC-2016-4472', 'Dermatology', 10,
          'Experienced in itchy-skin cases, nutrition plans, vaccination schedules, and senior pet wellness.',
          'Paws & Pulse Veterinary Care', 'House 21, Road 44, Gulshan 2',
          'Dhaka', 'Bangladesh', 1200, 2, true, true, 1, 4.8, 18, true,
          30, 12, v_now, false)
  ON CONFLICT (user_id) DO NOTHING
  RETURNING id INTO doc_nusrat;

  IF doc_nusrat IS NULL THEN
    doc_nusrat := (SELECT id FROM doctors WHERE user_id = u_nusrat);
  END IF;

  INSERT INTO doctor_specialties (doctor_id, specialty_id) VALUES
    (doc_nusrat, s_general),
    (doc_nusrat, s_dermatology)
  ON CONFLICT DO NOTHING;

  INSERT INTO doctor_animal_types (doctor_id, animal_type) VALUES
    (doc_nusrat, 2),  -- Cat
    (doc_nusrat, 1),  -- Dog
    (doc_nusrat, 4)   -- Rabbit
  ON CONFLICT DO NOTHING;

  INSERT INTO doctor_availabilities (id, doctor_id, day_of_week, start_time,
                                     end_time, slot_minutes, consultation_type) VALUES
    (gen_random_uuid(), doc_nusrat, 0, '09:00', '13:00', 30, 1),  -- Sunday, Offline
    (gen_random_uuid(), doc_nusrat, 2, '15:00', '19:00', 30, 2),  -- Tuesday, Both
    (gen_random_uuid(), doc_nusrat, 4, '10:00', '14:00', 30, 0);  -- Thursday, Online

  -- ---------- ADOPTION LISTINGS ------------------------------------------
  INSERT INTO adoption_listings (id, owner_id, pet_id, title, pet_name, description,
                                 animal_type, breed, age_months, gender, size, color,
                                 vaccinated, vaccination_details, neutered_spayed,
                                 health_condition, behavior_notes, good_with_children,
                                 good_with_other_pets, location, adoption_fee,
                                 reason_for_listing, contact_preference, status,
                                 submitted_at, decided_at, decided_by_user_id,
                                 created_at, is_deleted)
  VALUES
    (list_luna, u_liza, pet_luna, 'Gentle calico Luna needs a quiet home', 'Luna',
     'Luna was rescued near Mirpur and has settled into indoor life. She is affectionate after a short warm-up period.',
     2, 'Calico', 14, 2, 2, 'Calico',
     true, 'FVRCP and rabies up to date.', true,
     'Healthy; mild food sensitivity.', 'Quiet, litter-trained, prefers calm spaces.',
     true, true, 'Mirpur, Dhaka', 1200,
     'Rescued foster cat ready for permanent adoption.', 0, 2,
     v_now - interval '8 days', v_now - interval '7 days', u_admin,
     v_now, false),
    (list_tara, u_omar, NULL, 'Playful mixed-breed puppy already adopted', 'Tara',
     'Tara found a home after a home-check and trial weekend.',
     1, 'Local mixed breed', 5, 2, 3, 'Brown and white',
     true, NULL, false, NULL, NULL, NULL, NULL, 'Savar, Dhaka', 0, NULL, 1, 4,
     v_now - interval '24 days', v_now - interval '23 days', u_admin,
     v_now, false)
  ON CONFLICT (id) DO NOTHING;

  -- Mark Tara as adopted by Arif.
  UPDATE adoption_listings
    SET adopted_at = v_now - interval '12 days',
        adopted_by_user_id = u_arif
    WHERE id = list_tara;

  INSERT INTO adoption_listing_photos (id, adoption_listing_id, url, order_index) VALUES
    (gen_random_uuid(), list_luna,
     'https://images.unsplash.com/photo-1573865526739-10659fec78a5?auto=format&fit=crop&w=900&q=80', 0),
    (gen_random_uuid(), list_tara,
     'https://images.unsplash.com/photo-1583511655857-d19b40a7a54e?auto=format&fit=crop&w=900&q=80', 0);

  -- ---------- SERVICE PROVIDER -------------------------------------------
  INSERT INTO service_providers (id, user_id, provider_type, business_name, about,
                                 address, city, country, phone_number, base_price,
                                 approval_status, rating_average, rating_count,
                                 created_at, is_deleted)
  VALUES (svc_rima, u_rima, 0, 'Rima''s Gentle Grooming',
          'Low-stress grooming for cats, senior dogs, and first-time appointments.',
          'Tajmahal Road, Mohammadpur', 'Dhaka', 'Bangladesh', '+8801314550606',
          1500, 1, 4.9, 22, v_now, false)
  ON CONFLICT (id) DO NOTHING;

  -- ---------- POSTS (community feed) ------------------------------------
  INSERT INTO posts (id, author_id, content, animal_type, location, is_hidden,
                     created_at, is_deleted) VALUES
    (post1, u_maya,
     'Milo finished his follow-up visit today. The flea allergy plan is working and his coat is finally growing back evenly.',
     2, 'Dhanmondi, Dhaka', false, v_now - interval '6 hours', false),
    (post2, u_liza,
     'Luna is ready to meet patient adopters this week. She loves window naps and quiet evenings.',
     2, 'Mirpur, Dhaka', false, v_now - interval '1 day', false),
    (post3, u_tanvir,
     'Fresh stock of sensitive-skin grooming supplies arrived today. We can bundle them with food orders inside Dhaka.',
     1, 'Uttara, Dhaka', false, v_now - interval '2 days', false),
    (post4, u_arif,
     'First training session at the park went great — Buddy is starting to come back on command. Tips from other dog parents welcome!',
     1, 'Banani, Dhaka', false, v_now - interval '3 days', false);

  INSERT INTO post_media (id, post_id, url, media_type, order_index) VALUES
    (gen_random_uuid(), post1,
     'https://images.unsplash.com/photo-1574144611937-0df059b5ef3e?auto=format&fit=crop&w=900&q=80',
     'image', 0),
    (gen_random_uuid(), post2,
     'https://images.unsplash.com/photo-1573865526739-10659fec78a5?auto=format&fit=crop&w=900&q=80',
     'image', 0),
    (gen_random_uuid(), post3,
     'https://images.unsplash.com/photo-1616190264687-b7ebf1a391cd?auto=format&fit=crop&w=900&q=80',
     'image', 0),
    (gen_random_uuid(), post4,
     'https://images.unsplash.com/photo-1552053831-71594a27632d?auto=format&fit=crop&w=900&q=80',
     'image', 0);

  RAISE NOTICE 'Demo data seeded: 4 pets, 1 store, 4 products, 1 doctor, 2 adoption listings, 1 service provider, 4 posts.';
END;
$$;

COMMIT;
