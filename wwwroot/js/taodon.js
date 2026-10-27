// Màn hình tạo đơn tại quầy cho khách không dùng điện thoại.
(() => {
  'use strict';
  const $ = (chon) => document.querySelector(chon);
  const cauHinh = JSON.parse($('#pos-data').textContent);
  const dinhDangTien = (so) => Math.round(so).toString().replace(/\B(?=(\d{3})+(?!\d))/g, '.') + 'đ';
  const thoatHtml = (chuoi) => String(chuoi ?? '').replace(/[&<>"']/g, (k) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[k]));
  const boDau = (chuoi) => String(chuoi ?? '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase();

  let gioHang = [];   // đơn đang tạo (không lưu lại giữa các lần)
  const tienMotDong = (dong) => (dong.gia + dong.danhSachTopping.reduce((tong, t) => tong + t.gia, 0)) * dong.soLuong;
  const layGiaTheoSize = (maMonAn) => (cauHinh.giaTheoSize && cauHinh.giaTheoSize[String(maMonAn)]) || [];

  function hienThongBao(noiDung) {
    const the = document.createElement('div');
    the.className = 'toast';
    the.textContent = noiDung;
    $('#toasts').appendChild(the);
    setTimeout(() => the.remove(), 5000);
  }

  // ---------- Giỏ ----------
  function veLaiGio() {
    const tong = gioHang.reduce((t, d) => t + tienMotDong(d), 0);
    $('#posTong').textContent = dinhDangTien(tong);
    $('#posGui').disabled = gioHang.length === 0;
    $('#posItems').innerHTML = gioHang.length
      ? gioHang.map((dong, i) => {
          const tuyChon = [dong.tenKichCo ? 'Size ' + dong.tenKichCo : null, dong.tenMucDa, dong.tenMucDuong, ...dong.danhSachTopping.map((t) => '+ ' + t.ten)].filter(Boolean).join(', ');
          return `<div class="cart-line"><div class="n">${thoatHtml(dong.ten)}</div><div class="lt">${dinhDangTien(tienMotDong(dong))}</div>
            ${tuyChon ? `<div class="opts">${thoatHtml(tuyChon)}</div>` : ''}${dong.ghiChu ? `<div class="opts">Ghi chú: ${thoatHtml(dong.ghiChu)}</div>` : ''}
            <div class="qty"><button type="button" data-hd="giam" data-vt="${i}" aria-label="Giảm">−</button><span>${dong.soLuong}</span>
            <button type="button" data-hd="tang" data-vt="${i}" aria-label="Tăng">+</button>
            <button type="button" class="rm" data-hd="xoa" data-vt="${i}">Xóa</button></div></div>`;
        }).join('')
      : '<div class="cart-empty" style="padding:1.2rem 0">Chưa có món nào. Bấm vào món bên trái để thêm.</div>';
  }

  function themVaoGio(dongMoi) {
    const khoa = [dongMoi.maMonAn, dongMoi.maKichCo, dongMoi.maMucDa, dongMoi.maMucDuong, dongMoi.danhSachTopping.map((t) => t.ma).sort().join('-'), dongMoi.ghiChu].join('|');
    const daCo = gioHang.find((d) => d.khoa === khoa);
    if (daCo) daCo.soLuong = Math.min(20, daCo.soLuong + dongMoi.soLuong); else gioHang.push({ ...dongMoi, khoa });
    veLaiGio();
  }

  $('#posItems').addEventListener('click', (e) => {
    const nut = e.target.closest('button[data-hd]');
    if (!nut) return;
    const vt = +nut.dataset.vt;
    if (nut.dataset.hd === 'tang') gioHang[vt].soLuong = Math.min(20, gioHang[vt].soLuong + 1);
    if (nut.dataset.hd === 'giam') gioHang[vt].soLuong = Math.max(1, gioHang[vt].soLuong - 1);
    if (nut.dataset.hd === 'xoa') gioHang.splice(vt, 1);
    veLaiGio();
  });

  // ---------- Hộp tùy chọn món ----------
  const hop = $('#optDialog');
  let monChon = null, soLuongChon = 1;
  const sizeChon = () => {
    if (!monChon.coSize) return null;
    const radio = document.querySelector('#sizeChoices input:checked');
    return radio ? { ma: +radio.value, ten: radio.dataset.ten, gia: +radio.dataset.gia } : null;
  };
  const giaMonChon = () => { const s = sizeChon(); return s ? s.gia : monChon.gia; };
  const tinhTienChon = () => (giaMonChon() + [...document.querySelectorAll('#topChoices input:checked')].reduce((t, x) => t + +x.dataset.gia, 0)) * soLuongChon;
  const capNhatNut = () => { $('#optQty').textContent = soLuongChon; $('#optPrice').textContent = dinhDangTien(giaMonChon()); $('#optAdd').textContent = 'Thêm vào đơn - ' + dinhDangTien(tinhTienChon()); };

  document.querySelectorAll('.pos-o').forEach((o) => o.addEventListener('click', () => {
    const sizes = layGiaTheoSize(+o.dataset.ma);
    const coTuyChonDoUong = o.dataset.tuyChon === '1';
    monChon = { ma: +o.dataset.ma, ten: o.dataset.ten, gia: +o.dataset.gia, coSize: sizes.length > 0, sizes };
    if (!coTuyChonDoUong && !monChon.coSize) {
      themVaoGio({ maMonAn: monChon.ma, ten: monChon.ten, gia: monChon.gia, soLuong: 1, maKichCo: null, tenKichCo: '', maMucDa: null, tenMucDa: '', maMucDuong: null, tenMucDuong: '', danhSachTopping: [], ghiChu: '' });
      return;
    }
    soLuongChon = 1;
    $('#optName').textContent = monChon.ten;
    $('#optNote').value = '';

    $('#sizeFieldset').hidden = !monChon.coSize;
    if (monChon.coSize) {
      $('#sizeChoices').innerHTML = sizes.map((x, i) => `<label class="choice"><input type="radio" name="kichCo" value="${x.ma}" data-ten="${thoatHtml(x.ten)}" data-gia="${x.gia}" ${i === 0 ? 'checked' : ''} /><span>${thoatHtml(x.ten)} - ${dinhDangTien(x.gia)}</span></label>`).join('');
    }
    $('#iceChoices').closest('fieldset').hidden = !coTuyChonDoUong;
    $('#sugarChoices').closest('fieldset').hidden = !coTuyChonDoUong;
    $('#topChoices').closest('fieldset').hidden = !coTuyChonDoUong;
    if (coTuyChonDoUong) {
      $('#iceChoices').innerHTML = cauHinh.danhSachMucDa.map((x, i) => `<label class="choice"><input type="radio" name="mucDa" value="${x.ma}" data-ten="${thoatHtml(x.ten)}" ${i === 0 ? 'checked' : ''} /><span>${thoatHtml(x.ten)}</span></label>`).join('');
      $('#sugarChoices').innerHTML = cauHinh.danhSachMucDuong.map((x, i) => `<label class="choice"><input type="radio" name="mucDuong" value="${x.ma}" data-ten="${thoatHtml(x.ten)}" ${i === 0 ? 'checked' : ''} /><span>${thoatHtml(x.ten)}</span></label>`).join('');
      $('#topChoices').innerHTML = cauHinh.danhSachTopping.length
        ? cauHinh.danhSachTopping.map((t) => `<label class="choice"><input type="checkbox" value="${t.ma}" data-ten="${thoatHtml(t.ten)}" data-gia="${t.gia}" /><span>${thoatHtml(t.ten)} +${dinhDangTien(t.gia)}</span></label>`).join('')
        : '<span class="muted">Hiện chưa có topping.</span>';
    }
    capNhatNut();
    hop.showModal();
  }));
  hop.addEventListener('change', capNhatNut);
  $('#optMinus').addEventListener('click', () => { soLuongChon = Math.max(1, soLuongChon - 1); capNhatNut(); });
  $('#optPlus').addEventListener('click', () => { soLuongChon = Math.min(20, soLuongChon + 1); capNhatNut(); });
  $('#optCancel').addEventListener('click', () => hop.close());
  hop.addEventListener('click', (e) => { if (e.target === hop) hop.close(); });
  $('#optForm').addEventListener('submit', (e) => {
    e.preventDefault();
    if (monChon.coSize && !sizeChon()) return;
    const mucDa = document.querySelector('#iceChoices input:checked'), mucDuong = document.querySelector('#sugarChoices input:checked');
    const s = sizeChon();
    themVaoGio({
      maMonAn: monChon.ma, ten: monChon.ten, gia: giaMonChon(), soLuong: soLuongChon,
      maKichCo: s ? s.ma : null, tenKichCo: s ? s.ten : '',
      maMucDa: mucDa ? +mucDa.value : null, tenMucDa: mucDa ? mucDa.dataset.ten : '',
      maMucDuong: mucDuong ? +mucDuong.value : null, tenMucDuong: mucDuong ? mucDuong.dataset.ten : '',
      danhSachTopping: [...document.querySelectorAll('#topChoices input:checked')].slice(0, 5).map((x) => ({ ma: +x.value, ten: x.dataset.ten, gia: +x.dataset.gia })),
      ghiChu: $('#optNote').value.trim()
    });
    hop.close();
  });

  // ---------- Tìm món (không cần gõ dấu) ----------
  $('#posTim').addEventListener('input', (e) => {
    const tuKhoa = boDau(e.target.value.trim());
    let coKetQua = false;
    document.querySelectorAll('.pos-nhom').forEach((nhom) => {
      let conMon = false;
      nhom.querySelectorAll('.pos-o').forEach((o) => {
        const khop = !tuKhoa || boDau(o.dataset.tim).includes(tuKhoa);
        o.hidden = !khop;
        if (khop) conMon = true;
      });
      nhom.hidden = !conMon;
      if (conMon) coKetQua = true;
    });
    $('#posKhongThay').classList.toggle('hidden', coKetQua);
  });

  // ---------- Nơi phục vụ và thanh toán ----------
  // Khách mang đi bắt buộc thu tiền ngay; khách ngồi bàn được chọn "Thu sau".
  const oBan = $('#posBan'), nhanThuSau = $('#nhanThuSau'), oThuSau = nhanThuSau.querySelector('input');
  function capNhatThanhToan() {
    const mangDi = oBan.value === '';
    oThuSau.disabled = mangDi;
    nhanThuSau.classList.toggle('vo-hieu', mangDi);
    if (mangDi && oThuSau.checked) document.querySelector('input[name="pay"][value="1"]').checked = true;
    $('#ghiChuThanhToan').textContent = mangDi
      ? 'Khách mang đi phải thanh toán ngay trước khi làm.'
      : 'Khách ngồi bàn có thể thu sau khi dùng xong, hoặc thu ngay.';
  }
  oBan.addEventListener('change', capNhatThanhToan);
  capNhatThanhToan();

  // ---------- Gửi đơn ----------
  $('#posGui').addEventListener('click', async () => {
    const nut = $('#posGui'), thongBao = $('#posMsg');
    thongBao.textContent = '';
    nut.disabled = true;
    nut.textContent = 'Đang tạo đơn...';
    const pay = +document.querySelector('input[name="pay"]:checked').value;
    const ketQua = await guiJson('/ThuNgan/TaoDonTaiQuay', {
      maBan: oBan.value === '' ? null : +oBan.value,
      tenKhach: $('#posTen').value, ghiChu: $('#posGhiChu').value,
      soDienThoai: $('#posSdt').value.trim(), maGiamGia: $('#posMa').value.trim() || null,
      maPhuongThuc: pay === 0 ? null : pay,
      danhSachMon: gioHang.map((d) => ({ maMonAn: d.maMonAn, soLuong: d.soLuong, maKichCo: d.maKichCo, maMucDa: d.maMucDa, maMucDuong: d.maMucDuong, danhSachMaTopping: d.danhSachTopping.map((t) => t.ma), ghiChu: d.ghiChu }))
    });
    nut.textContent = 'Tạo đơn và bắt đầu làm';
    if (!ketQua.thanhCong) { thongBao.textContent = ketQua.thongBao || 'Chưa tạo được đơn, vui lòng thử lại.'; nut.disabled = gioHang.length === 0; return; }
    gioHang = [];
    $('#posTen').value = ''; $('#posGhiChu').value = ''; $('#posSdt').value = ''; $('#posMa').value = '';
    veLaiGio();
    if (!ketQua.canChuyenKhoan) {
      hienThongBao(`Đã tạo đơn ${ketQua.maTheoDoi} (${ketQua.noiPhucVu}), ${dinhDangTien(ketQua.tongTien)}${ketQua.daThu ? ', đã thu tiền' : ', thu sau'}`);
      return;
    }

    // Chuyển khoản: đơn đang "Chờ xác nhận". Hiện mã VietQR cho khách quét; thu ngân kiểm tra tiền đã vào tài khoản rồi mới xác nhận.
    hienThongBao(`Đã tạo đơn ${ketQua.maTheoDoi} (${ketQua.noiPhucVu}), đang chờ khách chuyển khoản`);
    if (!(await hienQrChuyenKhoan({ maDonHang: ketQua.maDonHang }))) {
      hienThongBao(`Đơn ${ketQua.maTheoDoi} chưa thu chuyển khoản, đang chờ ở Bảng đơn hàng.`);
      return;
    }
    const daThu = await guiJson('/ThuNgan/DanhDauDaThanhToan', { maDonHang: ketQua.maDonHang, maPhuongThuc: 2 });
    if (!daThu.thanhCong) { hienThongBao(daThu.thongBao || 'Chưa ghi nhận được khoản thu.'); return; }
    const batDau = await guiJson('/ThuNgan/CapNhatTrangThai', { maDonHang: ketQua.maDonHang, maTrangThai: 2 });
    hienThongBao(batDau.thanhCong ? `Đã thu chuyển khoản đơn ${ketQua.maTheoDoi}, bắt đầu làm.` : 'Đã thu tiền nhưng chưa chuyển sang Đang làm, hãy kiểm tra Bảng đơn hàng.');
  });

  veLaiGio();
})();
