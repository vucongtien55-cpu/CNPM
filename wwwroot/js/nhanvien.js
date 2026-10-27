// Hàm dùng chung cho giao diện nhân viên.
window.guiJson = async (duongDan, duLieu) => {
  const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
  try {
    const phanHoi = await fetch(duongDan, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', RequestVerificationToken: token },
      body: JSON.stringify(duLieu)
    });
    let ketQua = {};
    try { ketQua = await phanHoi.json(); } catch { /* không có nội dung JSON */ }
    return { ...ketQua, thanhCong: phanHoi.ok && ketQua.thanhCong !== false };
  } catch {
    return { thanhCong: false, thongBao: 'Mất kết nối tới máy chủ.' };
  }
};

// ---------- Thu chuyển khoản bằng mã VietQR ----------
// hienQrChuyenKhoan({ maDonHang }) hoặc hienQrChuyenKhoan({ maBan }) -> Promise<true|false>
// true = thu ngân bấm "Đã nhận tiền" (sau khi tự kiểm tra tài khoản ngân hàng); false = đóng hộp, chưa thu.
window.hienQrChuyenKhoan = (thamSo) => {
  const hop = document.getElementById('hopQrChuyenKhoan');
  const $ = (id) => document.getElementById(id);
  const dinhDangTien = (so) => Math.round(so).toString().replace(/\B(?=(\d{3})+(?!\d))/g, '.') + 'đ';

  return new Promise((xong) => {
    let daXong = false;
    const ketThuc = (dongY) => { if (daXong) return; daXong = true; hop.close(); xong(dongY); };

    // Đặt lại giao diện mỗi lần mở
    $('qrTieuDe').textContent = 'Thu chuyển khoản';
    $('qrSoTien').textContent = 'Đang tải...';
    $('qrLoi').textContent = '';
    $('qrAnh').hidden = true; $('qrAnh').removeAttribute('src');
    ['qrNganHang', 'qrSoTk', 'qrChuTk', 'qrNoiDung'].forEach((id) => { $(id).textContent = ''; });
    $('qrXacNhan').disabled = true;

    $('qrXacNhan').onclick = () => ketThuc(true);
    $('qrDong').onclick = () => ketThuc(false);
    hop.oncancel = (e) => { e.preventDefault(); ketThuc(false); };          // phím Esc
    hop.onclick = (e) => { if (e.target === hop) ketThuc(false); };         // bấm ra ngoài
    hop.showModal();

    (async () => {
      const tuKhoa = new URLSearchParams();
      if (thamSo.maDonHang) tuKhoa.set('maDonHang', thamSo.maDonHang);
      if (thamSo.maBan) tuKhoa.set('maBan', thamSo.maBan);
      try {
        const phanHoi = await fetch('/ThuNgan/ThongTinQr?' + tuKhoa.toString(), { cache: 'no-store' });
        const duLieu = await phanHoi.json().catch(() => ({}));
        if (!phanHoi.ok || !duLieu.thanhCong) { $('qrSoTien').textContent = ''; $('qrLoi').textContent = duLieu.thongBao || 'Không lấy được thông tin thanh toán.'; return; }

        $('qrTieuDe').textContent = duLieu.tieuDe;
        $('qrSoTien').textContent = dinhDangTien(duLieu.soTien);
        $('qrNganHang').textContent = duLieu.tenNganHang || '-';
        $('qrSoTk').textContent = duLieu.soTaiKhoan || '-';
        $('qrChuTk').textContent = duLieu.tenChuTaiKhoan || '-';
        $('qrNoiDung').textContent = duLieu.noiDung;
        if (duLieu.daCauHinh && duLieu.duongDanAnh) {
          $('qrAnh').src = duLieu.duongDanAnh;
          $('qrAnh').hidden = false;
        } else {
          $('qrLoi').textContent = 'Chưa cấu hình tài khoản ngân hàng (mục "NganHang" trong appsettings.json) nên chưa có mã QR. Bạn vẫn có thể xác nhận thủ công khi đã nhận được tiền.';
        }
        $('qrXacNhan').disabled = false;
      } catch {
        $('qrSoTien').textContent = '';
        $('qrLoi').textContent = 'Mất kết nối tới máy chủ. Hãy đóng hộp này và thử lại.';
      }
    })();
  });
};

// Nút "Chuyển khoản" nằm trong form (trang Lịch sử, Đối soát): hiện mã QR và chỉ gửi form khi thu ngân xác nhận đã nhận tiền.
document.addEventListener('click', async (e) => {
  const nut = e.target.closest('button[data-qr-don]');
  if (!nut) return;
  e.preventDefault();
  if (!(await window.hienQrChuyenKhoan({ maDonHang: +nut.dataset.qrDon }))) return;
  const form = nut.closest('form');
  const truongAn = document.createElement('input');
  truongAn.type = 'hidden'; truongAn.name = nut.name; truongAn.value = nut.value;
  form.appendChild(truongAn);
  form.submit();
});

// ---------- Hủy đơn (dùng chung mọi trang nhân viên) ----------
// hienHuyDon({ maDonHang, moTa, daThanhToan, soTien }) -> Promise<true|false>
window.hienHuyDon = (thongTin) => new Promise((xong) => {
  const hop = document.getElementById('hopHuyDon');
  const $ = (id) => document.getElementById(id);
  const dinhDangTien = (so) => Math.round(so).toString().replace(/\B(?=(\d{3})+(?!\d))/g, '.') + 'đ';
  let daXong = false;
  const ketThuc = (ok) => { if (daXong) return; daXong = true; hop.close(); xong(ok); };

  $('huyMoTaDon').textContent = thongTin.moTa || '';
  $('huyLyDo').innerHTML = (window.__lyDoHuyDon || ['Khách đổi ý', 'Khác']).map((l) => `<option value="${l}">${l}</option>`).join('');
  $('huyGhiChu').value = '';
  $('huyLoi').textContent = '';
  $('huyHoanKho').checked = true;
  $('huyDaHoanTien').checked = false;
  $('huyKhungHoanTien').hidden = !thongTin.daThanhToan;
  if (thongTin.daThanhToan) $('huySoTienHoan').textContent = dinhDangTien(thongTin.soTien || 0);

  const capNhatKhungKhac = () => { $('huyKhungKhac').hidden = $('huyLyDo').value !== 'Khác'; };
  $('huyLyDo').onchange = capNhatKhungKhac;
  capNhatKhungKhac();

  $('huyDong').onclick = () => ketThuc(false);
  hop.oncancel = (e) => { e.preventDefault(); ketThuc(false); };
  hop.onclick = (e) => { if (e.target === hop) ketThuc(false); };

  document.getElementById('formHuyDon').onsubmit = async (e) => {
    e.preventDefault();
    if (thongTin.daThanhToan && !$('huyDaHoanTien').checked) { $('huyLoi').textContent = 'Vui lòng xác nhận đã hoàn tiền cho khách.'; return; }
    const ketQua = await guiJson('/ThuNgan/HuyDon', {
      maDonHang: thongTin.maDonHang, lyDo: $('huyLyDo').value, ghiChu: $('huyGhiChu').value,
      hoanKho: $('huyHoanKho').checked, daHoanTien: $('huyDaHoanTien').checked
    });
    if (!ketQua.thanhCong) { $('huyLoi').textContent = ketQua.thongBao || 'Chưa hủy được đơn.'; return; }
    ketThuc(true);
  };
  // Tải sẵn danh sách lý do 1 lần rồi dùng lại
  if (!window.__lyDoHuyDon) {
    fetch('/ThuNgan/DanhSachLyDoHuy').then((r) => r.json()).then((ds) => { window.__lyDoHuyDon = ds; $('huyLyDo').innerHTML = ds.map((l) => `<option value="${l}">${l}</option>`).join(''); capNhatKhungKhac(); }).catch(() => {});
  }
  hop.showModal();
});

// Nút hủy đơn gắn data-huy-don + data-huy-mo-ta (+ data-huy-da-thanh-toan, data-huy-so-tien) ở bất kỳ trang nào, không cần viết JS riêng.
document.addEventListener('click', async (e) => {
  const nut = e.target.closest('[data-huy-don]');
  if (!nut) return;
  e.preventDefault();
  const ok = await window.hienHuyDon({
    maDonHang: +nut.dataset.huyDon, moTa: nut.dataset.huyMoTa,
    daThanhToan: nut.dataset.huyDaThanhToan === '1', soTien: +(nut.dataset.huySoTien || 0)
  });
  if (ok && nut.dataset.huyTaiLai !== '0' && typeof window.taiLaiSauKhiHuy === 'function') window.taiLaiSauKhiHuy();
  else if (ok) location.reload();
});
