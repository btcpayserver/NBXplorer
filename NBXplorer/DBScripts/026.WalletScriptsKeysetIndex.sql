CREATE INDEX IF NOT EXISTS wallets_scripts_wallet_id_code_script_idx
	ON wallets_scripts (wallet_id, code, script);
