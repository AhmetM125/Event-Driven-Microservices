-- ==============================================================================
-- Database Initialization Script for EventDrivenShop Local Development
-- Creates logically separate databases to maintain strict database-per-service boundaries.
-- ==============================================================================

SELECT 'CREATE DATABASE orders_db'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'orders_db')\gexec

SELECT 'CREATE DATABASE payments_db'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'payments_db')\gexec

SELECT 'CREATE DATABASE notifications_db'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'notifications_db')\gexec
